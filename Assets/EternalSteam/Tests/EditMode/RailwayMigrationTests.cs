using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using EternalSteam.OpenWorld;
using UnityEngine;
namespace EternalSteam.Tests
{
    public sealed class RailwayMigrationTests
    {
        string root,id;JsonSaveStore store;
        [SetUp] public void Setup(){root=Path.Combine(Path.GetTempPath(),"eternal-railway-migration-"+Guid.NewGuid().ToString("N"));id=Guid.NewGuid().ToString("N");store=new JsonSaveStore(root);}
        [TearDown] public void Cleanup(){if(Directory.Exists(root))Directory.Delete(root,true);}
        [Test] public void InterruptedWriteAndArchiveRetryKeepOriginalUntilSuccessfulCommit()
        {
            const string source="{\"version\":1,\"iron\":1234}";store.Write(id,source);string original=File.ReadAllText(store.CurrentPath);store.PreserveMigrationSource(source);
            File.WriteAllText(store.CurrentPath+".tmp","interrupted v2 write");Assert.That(store.Read().Payload,Is.EqualTo(source));store.PreserveMigrationSource(source);
            Assert.That(Directory.GetDirectories(Path.Combine(root,"migrations")).Length,Is.EqualTo(1));store.Write(id,"{\"version\":2,\"mainIron\":1234}");store.PreserveMigrationSource(source);
            var archive=Directory.GetDirectories(Path.Combine(root,"migrations"))[0];Assert.That(File.ReadAllText(Path.Combine(archive,"current.json")),Is.EqualTo(original));Assert.That(File.ReadAllText(Path.Combine(archive,"source-payload.json")),Is.EqualTo(source));
        }
        [Test] public void ArchiveFailureLeavesOriginalReadable(){store.Write(id,"v1");File.WriteAllText(Path.Combine(root,"migrations"),"blocked by file");Assert.Throws<IOException>(()=>store.PreserveMigrationSource("v1"));Assert.That(store.Read().Payload,Is.EqualTo("v1"));}
        [Test] public void FailedRunCannotBeRevivedByMigrationOrBackup(){store.Write(id,"v1");store.PreserveMigrationSource("v1");store.Write(id,"v2");store.MarkFailed(id);Assert.That(store.Read().Blocked,Is.True);Assert.Throws<InvalidOperationException>(()=>store.Write(id,"v2 retry"));}
        [Test] public void V1StockMovesToMainOnceWithoutClippingOverflowOrCreditingSubBase()
        {
            string sub=Guid.NewGuid().ToString("N");
            var source=new List<ResourceBank.Stock>{new(){id="iron",amount=1234,capacity=1000},new(){id="coal",amount=7,capacity=50}};
            var capacities=new List<ResourceBank.Stock>{new(){id="iron",amount=0,capacity=1000},new(){id="coal",amount=0,capacity=75}};
            var first=SingleMapV1Migration.Inventories(source,capacities,new[]{sub,id},id);
            var repeated=SingleMapV1Migration.Inventories(source,capacities,new[]{sub,id},id);
            Assert.That(first.SelectMany(record=>record.stocks).Where(stock=>stock.id=="iron").Sum(stock=>stock.amount),Is.EqualTo(1234));
            Assert.That(first.Single(record=>record.baseId==id).stocks.Single(stock=>stock.id=="iron").amount,Is.EqualTo(1234));
            Assert.That(first.Single(record=>record.baseId==id).stocks.Single(stock=>stock.id=="iron").capacity,Is.EqualTo(1000));
            Assert.That(first.Single(record=>record.baseId==sub).stocks.All(stock=>stock.amount==0),Is.True);
            Assert.That(first.Single(record=>record.baseId==id).stocks.Single(stock=>stock.id=="coal").capacity,Is.EqualTo(75));
            Assert.That(repeated.SelectMany(record=>record.stocks).Sum(stock=>stock.amount),Is.EqualTo(1241));
            Assert.That(source.Single(stock=>stock.id=="iron").amount,Is.EqualTo(1234));
        }
        [Test] public void V1StockRejectsAmbiguousBaseOrResourceMapping()
        {
            var source=new List<ResourceBank.Stock>{new(){id="iron",amount=10,capacity=20}};
            Assert.Throws<ArgumentException>(()=>SingleMapV1Migration.Inventories(source,source,new[]{id,id},id));
            Assert.Throws<ArgumentException>(()=>SingleMapV1Migration.Inventories(source,source,new[]{Guid.NewGuid().ToString("N")},id));
            Assert.Throws<ArgumentException>(()=>SingleMapV1Migration.Inventories(source,new List<ResourceBank.Stock>(),new[]{id},id));
        }
        [Test] public void OlderMapSlotsAreReportedWithoutReadingArchivesOrChangingSaves()
        {
            string saves=Path.Combine(root,"single-map");
            string current=Path.Combine(saves,"maps","current");
            string otherMap=Path.Combine(saves,"maps","other-map");
            new JsonSaveStore(saves).Write(Guid.NewGuid().ToString("N"),JsonUtility.ToJson(new SingleMapSnapshot{mapId="start-region",configuration="old-50"}));
            new JsonSaveStore(current).Write(Guid.NewGuid().ToString("N"),JsonUtility.ToJson(new SingleMapSnapshot{mapId="start-region",configuration="new-500"}));
            new JsonSaveStore(otherMap).Write(Guid.NewGuid().ToString("N"),JsonUtility.ToJson(new SingleMapSnapshot{mapId="different-region",configuration="old-other"}));
            string original=File.ReadAllText(Path.Combine(saves,"current.json"));
            Assert.That(SaveSlotDiscovery.CountOtherConfigurations(saves,current,"start-region","new-500"),Is.EqualTo(1));
            Assert.That(File.ReadAllText(Path.Combine(saves,"current.json")),Is.EqualTo(original));
            new JsonSaveStore(saves).Archive();
            Assert.That(SaveSlotDiscovery.CountOtherConfigurations(saves,current,"start-region","new-500"),Is.Zero);
        }
        [Test] public void CorruptOrFailedOlderSlotsAreNotAdvertisedAsAvailableProgress()
        {
            string saves=Path.Combine(root,"single-map");string current=Path.Combine(saves,"maps","current");
            Directory.CreateDirectory(saves);File.WriteAllText(Path.Combine(saves,"current.json"),"corrupt");
            Assert.That(SaveSlotDiscovery.CountOtherConfigurations(saves,current,"start-region","new-500"),Is.Zero);
            var old=new JsonSaveStore(saves);old.Write(id,JsonUtility.ToJson(new SingleMapSnapshot{mapId="start-region",configuration="old-50"}));old.MarkFailed(id);
            Assert.That(SaveSlotDiscovery.CountOtherConfigurations(saves,current,"start-region","new-500"),Is.Zero);
        }
    }
}
