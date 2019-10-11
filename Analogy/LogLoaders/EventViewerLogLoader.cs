﻿using Philips.Analogy.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Philips.Analogy.Interfaces.DataTypes;

namespace Philips.Analogy.LogLoaders
{
    public class EventViewerLogLoader
    {
        private string[] seperators = new[] { "Severity: ", ", Category: ", ", MessageID: ", ", Message: " };
        private CancellationToken Token { get; }
        
        public EventViewerLogLoader(CancellationToken token)
        {
            Token = token;
        }
        public async Task<IEnumerable<AnalogyLogMessage>> ReadFromFile(string fileName, ILogMessageCreatedHandler logWindow)
        {
            if (!File.Exists(fileName))
            {
                await Task.CompletedTask;
                return new List<AnalogyLogMessage>();
            }

            FileProcessingManager.Instance.AddProcessingFile(fileName);
            List<AnalogyLogMessage> messages = new List<AnalogyLogMessage>();
            return await Task.Factory.StartNew(() =>
            {
                try
                {
                    using (var reader = new EventLogReader(fileName, PathType.FilePath))
                    {
                        EventRecord record;
                        while ((record = reader.ReadEvent()) != null)
                        {
                            if (Token.IsCancellationRequested)
                                break;
                            using (record)
                            {
                                AnalogyLogMessage m = new AnalogyLogMessage();
                                m.Date = record.TimeCreated ?? DateTime.MinValue;
                                m.Source = record.ProviderName;
                                m.Module = record.ProviderName;
                                m.Level = AnalogyLogLevel.Event;
                                m.ID = record.ActivityId ?? Guid.Empty;
                                m.ProcessID = record.ProcessId ?? 0;
                                m.FileName = fileName;
                                m.User = record.UserId?.Value;
                                string properties = string.Join(Environment.NewLine, record.Properties.Select(p => p.Value));
                                try
                                {
                                    m.Text = $"{record.MachineName} :({record.LogName}) - {record.FormatDescription()}{properties}{(record.ThreadId !=null ? " Thread id:" + record.ThreadId.Value : string.Empty)}";
                                    if (record.LevelDisplayName != null)
                                    {
                                        switch (record.LevelDisplayName)
                                        {
                                            case "Information":
                                                m.Level = AnalogyLogLevel.Event;
                                                break;
                                            case "Error":
                                                m.Level = AnalogyLogLevel.Error;
                                                break;
                                            case "Critical Error":
                                                m.Level = AnalogyLogLevel.Critical;
                                                break;
                                            case "Warning":
                                                m.Level = AnalogyLogLevel.Warning;
                                                break;
                                            case "Verbose":
                                                m.Level = AnalogyLogLevel.Verbose;
                                                break;
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                    var items = record.Properties[0].Value.ToString().Split(seperators, StringSplitOptions.RemoveEmptyEntries);
                                    if (items.Any() && items.Length == 4)
                                    {
                                        m.Text = $"{record.MachineName} :({record.LogName}) - {items[3]} . Message ID: {items[2]}";
                                        m.Category = items[1];
                                        switch (items[0])
                                        {
                                            case "Informational":
                                                m.Level = AnalogyLogLevel.Event;
                                                break;
                                            case "Information":
                                                m.Level = AnalogyLogLevel.Event;
                                                break;
                                            case "Error":
                                                m.Level = AnalogyLogLevel.Error;
                                                break;
                                            case "Critical":
                                                m.Level = AnalogyLogLevel.Critical;
                                                break;
                                            case "Critical Error":
                                                m.Level = AnalogyLogLevel.Critical;
                                                break;
                                            case "Warning":
                                                m.Level = AnalogyLogLevel.Warning;
                                                break;
                                            case "Verbose":
                                                m.Level = AnalogyLogLevel.Verbose;
                                                break;
                                            default:
                                                m.Level = AnalogyLogLevel.Event;
                                                break;
                                        }
                                    }
                                    else
                                    {
                                        m.Text = $"{record.MachineName} :({record.LogName}) - {record.FormatDescription()}{properties}{(record.ThreadId.HasValue ? " Thread id:" + record.ThreadId.Value : string.Empty)}";
                                        if (record.Level != null)
                                        {
                                            switch (record.Level.Value)
                                            {
                                                case 2:
                                                    m.Level = AnalogyLogLevel.Error;
                                                    break;
                                                case 3:
                                                    m.Level = AnalogyLogLevel.Warning;
                                                    break;
                                                case 4:
                                                    m.Level = AnalogyLogLevel.Event;
                                                    break;
                                                default:
                                                    m.Level = AnalogyLogLevel.Event;
                                                    break;
                                            }
                                        }

                                    }
                                }
                                messages.Add(m);
                                logWindow.AppendMessage(m, Utils.GetFileNameAsDataSource(fileName));
                            }
                        }
                    }

                }
                catch (Exception e)
                {
                    string fail = "Failed To parse: " + fileName + " Error:" + e;
                    AnalogyLogMessage m = new AnalogyLogMessage();
                    m.Text = fail;
                    m.Level = AnalogyLogLevel.Critical;
                    m.Class = AnalogyLogClass.General;
                    m.Source = "Analogy";
                    messages.Add(m);
                    logWindow.AppendMessages(messages, Utils.GetFileNameAsDataSource(fileName));
                }

                if (!messages.Any())
                {
                    AnalogyLogMessage empty = new AnalogyLogMessage($"File {fileName} is empty or corrupted", AnalogyLogLevel.Error, AnalogyLogClass.General, "Analogy","None");
                    messages.Add(empty);
                    logWindow.AppendMessage(empty, Utils.GetFileNameAsDataSource(fileName));
                }

                //logWindow.AppendMessages(messages, Utils.GetFileNameAsDataSource(fileName));               
                FileProcessingManager.Instance.DoneProcessingFile(messages, fileName);
                return messages;
            }, Token);
        }
    }
}
