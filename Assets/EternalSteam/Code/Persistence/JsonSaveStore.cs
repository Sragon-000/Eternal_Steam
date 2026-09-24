using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace EternalSteam
{
    public interface ISaveEligibility {bool CanSave(out string reason);}
    public interface ISaveStore {SaveRead Read();void Write(string runId,string payload);void MarkFailed(string runId);bool IsFailed(string runId);void Archive();}
    public sealed class SaveRead {public string Payload,RunId,Error;public bool Recovered,Blocked;}
    public sealed class IncompatibleSaveException:Exception {public IncompatibleSaveException(string message):base(message){}}
    [Serializable] sealed class SaveEnvelope {public int version=1;public string runId,payload,checksum;}
    public sealed class JsonSaveStore:ISaveStore
    {
        readonly string directory;
        public string CurrentPath=>Path.Combine(directory,"current.json");
        string NewGameMarker=>Path.Combine(directory,"new-game.pending");
        public string BackupPath=>Path.Combine(directory,"previous.json");
        public JsonSaveStore(string directory){this.directory=directory;}
        public static string Digest(string text){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));}
        static void CheckId(string id){if(!Guid.TryParseExact(id,"N",out _))throw new InvalidDataException("Invalid run ID");}
        string FailurePath(string id){CheckId(id);return Path.Combine(directory,"failures",id+".failed");}
        public bool IsFailed(string id)=>File.Exists(FailurePath(id));
        static SaveEnvelope Decode(string path){var info=new FileInfo(path);if(info.Length>64*1024*1024)throw new InvalidDataException("Save too large");var data=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path));if(data==null)throw new InvalidDataException("Missing save");if(data.version!=1)throw new IncompatibleSaveException("지원하지 않는 저장 버전입니다.");CheckId(data.runId);if(data.payload==null||data.checksum!=Digest(data.runId+"\n"+data.payload))throw new InvalidDataException("저장 파일 무결성 오류");return data;}
        public SaveRead Read()
        {
            if(File.Exists(NewGameMarker))return new SaveRead();
            foreach(var path in new[]{CurrentPath,BackupPath}){
                if(!File.Exists(path))continue;
                try{var data=Decode(path);if(IsFailed(data.runId))return new SaveRead{RunId=data.runId,Blocked=true,Error="패배한 진행입니다. 새 게임만 가능합니다."};return new SaveRead{Payload=data.payload,RunId=data.runId,Recovered=path==BackupPath};}
                catch(IncompatibleSaveException e){return new SaveRead{Blocked=true,Error=e.Message};}
                catch(Exception e){if(path==BackupPath||!File.Exists(BackupPath))return new SaveRead{Blocked=true,Error="저장을 읽을 수 없습니다: "+e.Message};}
            }return new SaveRead();
        }
        static void AtomicWrite(string path,string text,bool validate=false){Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";var bytes=Encoding.UTF8.GetBytes(text);using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}if(validate)Decode(temp);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
        public void Write(string runId,string payload)
        {
            CheckId(runId);if(IsFailed(runId))throw new InvalidOperationException("패배한 진행은 저장할 수 없습니다.");
            var data=new SaveEnvelope{runId=runId,payload=payload,checksum=Digest(runId+"\n"+payload)};
            Directory.CreateDirectory(directory);
            if(File.Exists(NewGameMarker)){foreach(var old in new[]{CurrentPath,BackupPath})if(File.Exists(old))File.Delete(old);}
            if(File.Exists(CurrentPath)){try{Decode(CurrentPath);AtomicWrite(BackupPath,File.ReadAllText(CurrentPath));}catch(InvalidDataException){/* Preserve the last known backup when the primary is corrupt. */}}
            AtomicWrite(CurrentPath,JsonUtility.ToJson(data,true),true);Decode(CurrentPath);if(File.Exists(NewGameMarker))File.Delete(NewGameMarker);
        }
        public void MarkFailed(string runId)=>AtomicWrite(FailurePath(runId),DateTime.UtcNow.ToString("O"));
        public void Archive()
        {
            string archive=Path.Combine(directory,"archive",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"));
            // Copy every existing record before removing the active slot. Fail closed on I/O errors.
            Directory.CreateDirectory(archive);
            foreach(var path in new[]{CurrentPath,BackupPath})if(File.Exists(path))File.Copy(path,Path.Combine(archive,Path.GetFileName(path)));
            AtomicWrite(NewGameMarker,archive);
            foreach(var path in new[]{CurrentPath,BackupPath})if(File.Exists(path))File.Delete(path);
        }
    }
}
