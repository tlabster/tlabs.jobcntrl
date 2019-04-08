﻿using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using Tlabs.Misc;
using Tlabs.Data.Serialize.Json;

namespace Tlabs.JobCntrl.Intern {

  /// <summary>Persists starter completion results as json file.</summary>
  public class StarterCompletionJsonPersister : IStarterCompletionPersister {
    /// <summary>Default <see cref="App.ContentRoot"/> relative persistence path.</summary>
    public const string DEFAULT_PERSISTENCE_PATH= "data/jobStartLog";
    static readonly Encoding INFO_ENCODING= Encoding.UTF8;
    const string CONTENT_TYPE= "text/json";
    private DirectoryInfo complLogDir;
    private object synchLog= new object();
    private bool logResObjects;
    private bool append;
    /// <summary>Default Ctor.</summary>
    public StarterCompletionJsonPersister() : this(DEFAULT_PERSISTENCE_PATH, logResObjects: false, append: false) { }

    /// <summary>Ctor from <paramref name="persistencePath"/>.</summary>
    public StarterCompletionJsonPersister(string persistencePath, bool logResObjects, bool append) {
      var complLogPath= Path.Combine(App.ContentRoot, persistencePath);
      this.complLogDir= new DirectoryInfo(complLogPath);
      this.complLogDir.Create();
      this.complLogDir.Refresh();
      this.logResObjects= logResObjects;
      this.append= append;
    }

    /// <summary>Event fired when a starter completion info has been persisted.</summary>
    public event Action<IStarterCompletionPersister, Model.Intern.IStarterCompletion, object> CompletionInfoPersisted;

    ///<inherit/>
    public Stream GetLastCompletionInfo(string starterName, out string contentType, out Encoding infoEncoding) {
      lock (synchLog) {
        contentType= CONTENT_TYPE;
        infoEncoding= INFO_ENCODING;
        var logFile= BuildFileInfo(starterName);
        return logFile.Exists
               ? new FileStream(logFile.FullName, FileMode.Open, FileAccess.Read)
               : null;
      }
    }

    ///<inherit/>
    public void StoreCompletionInfo(Model.Intern.IStarterCompletion starterCompletion) {
      FileInfo logFile;
      lock (synchLog) logFile= SerializeStarterCompletion(starterCompletion);

      CompletionInfoPersisted?.Invoke(this, starterCompletion, logFile);
    }

    private FileInfo BuildFileInfo(string starterName) {
      return new FileInfo(Path.Combine(complLogDir.FullName, starterName) + ".json");
    }

    private FileInfo SerializeStarterCompletion(Model.Intern.IStarterCompletion starterCompletion) {
      var logFile= BuildFileInfo(starterCompletion.StarterName);
      var json= JsonFormat.CreateDynSerializer();
      using (var strm= File.Open(logFile.FullName, append ? FileMode.Append : FileMode.Create)) {
        json.WriteObj(strm, buildStarterCompletion(starterCompletion));
        using (var wr= new StreamWriter(strm)) {
          if (append) wr.WriteLine(",\f");
        }
      }
      return logFile;
    }

    private object buildStarterCompletion(Model.Intern.IStarterCompletion starterCompletion) {
      return new {
        starter= starterCompletion.StarterName,
        time= starterCompletion.Time,
        jobResults= this.buildJobResults(starterCompletion.JobResults)
      };
    }

    private IEnumerable buildJobResults(IEnumerable<Model.IJobResult> jobResults) {
      foreach (var jbRes in jobResults) {
        var resObj=   logResObjects
                    ? jbRes.ResultObjects
                    : null;
        yield return new {
          job= jbRes.JobName,
          endTime= jbRes.EndAt,
          success= jbRes.IsSuccessful,
          message= jbRes.Message,
          resultObjs= resObj,
          log=   jbRes.ProcessingLog == null
               ? null
               : new {
                 hasProblem= jbRes.ProcessingLog.HasProblem,
                 entries= buildLogEntries(jbRes.ProcessingLog, jbRes.ProcessingLog.Entries)
               }
        };
      }
    }

    private IEnumerable buildLogEntries(Model.ILog log, IEnumerable<Model.ILogEntry> entries) {
      foreach (var ent in entries) {
        yield return new {
          lev= ent.Level.ToString(),
          time= string.Format("{0:HH:mm:ss,FFF}", log.EntryTime(ent)),
          step= ent.ProcessStep,
          msg= ent.Message
        };
      }
    }

  }//class
}
