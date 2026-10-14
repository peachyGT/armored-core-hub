using Hub.Core;
using System.Text.Json.Nodes;

static class ControlAuditTests
{
    public static void Register(Action<string,Action> test,string scratch,Catalog catalog)
    {
        void Reject(Action action){try{action();}catch(HubException){return;}throw new Exception("Expected a recoverable binding validation error");}
        test("Incomplete binding dictionaries and legacy imports fail with a recoverable error",()=>{
            Reject(()=>ControlProfiles.Resolve("pcsx2",new(){Keyboard=null!},true));
            Reject(()=>ControlProfiles.Resolve("rpcs3",new(){Imported=[null!]},false));
            Reject(()=>ControlProfiles.MigrateLegacy(new(){Community=new("Broken","",["fixture"],null!,[])}));
            Reject(()=>ControlProfiles.Validate(null!,true));
            var edition=catalog.Editions.First(e=>e.GameId=="ac2"&&e.Platform=="ps2");
            Reject(()=>ControlProfiles.Import(new(),new("Broken","",[edition.Id,null!],[],[]),edition));
        });
        test("Explicit null nested controls preserve damaged JSON and recover the previous library",()=>{
            var paths=new HubPaths(Path.Combine(scratch,"audit-null-controls"));var store=new StateStore(paths);
            var state=new HubState{Offline=true,Selections=new(){{"fixture",new(){EditionId="fixture"}}}};
            store.Save(state);state.Offline=false;store.Save(state);
            var file=paths.Managed("config","state.json");var json=JsonNode.Parse(File.ReadAllText(file))!;json["selections"]!["fixture"]!["controls"]!["keyboard"]=null;
            File.WriteAllText(file,json.ToJsonString());var recovered=store.Load();
            if(!recovered.Offline||recovered.Selections["fixture"].Controls.Keyboard==null||store.RecoveryNotice==null||!Directory.EnumerateFiles(paths.Managed("config"),"*.damaged-*").Any())throw new Exception("Incomplete nested controls bypassed state recovery");
        });
        test("Duplicate imported binding IDs are rejected instead of editing an ambiguous profile",()=>{
            var choice=new ControlChoice{Imported=[new("same",new("One","",["fixture"],[],[])),new("same",new("Two","",["fixture"],[],[]))]};
            Reject(()=>ControlProfiles.Resolve("pcsx2",choice,true));
        });
        test("Hostile SDL mappings reject within a bounded time instead of backtracking on the UI thread",()=>{
            var mapping="xinput,Hostile,a:"+string.Concat(Enumerable.Repeat(new string('b',80)+":",40))+"!";
            var watch=System.Diagnostics.Stopwatch.StartNew();Reject(()=>ControlProfiles.ValidateSdl(mapping));
            if(watch.Elapsed>TimeSpan.FromSeconds(2))throw new Exception("Malformed SDL validation exceeded its time bound");
        });
    }
}
