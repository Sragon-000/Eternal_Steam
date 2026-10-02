using System;
using System.Collections.Generic;
namespace EternalSteam
{
    /// <summary>Validate every payer before committing construction; failed commits never charge stock.</summary>
    public static class ConstructionPurchase
    {
        public sealed class Payment {public ResourceBank Bank;public string Resource;public double Amount;}
        public static bool TryCommit(IReadOnlyList<Payment> payments,Func<bool> commit,out string reason)
        {
            if(payments==null||commit==null){reason="건설 비용 또는 설치 명령이 없습니다.";return false;}
            reason=null;var totals=new Dictionary<(ResourceBank,string),double>();
            foreach(var p in payments){if(p==null||p.Bank==null||string.IsNullOrWhiteSpace(p.Resource)||!double.IsFinite(p.Amount)||p.Amount<0){reason="건설 비용 또는 지불 기지가 유효하지 않습니다.";return false;}
                var key=(p.Bank,p.Resource);totals.TryGetValue(key,out var previous);double sum=previous+p.Amount;if(!double.IsFinite(sum)){reason="건설 비용 범위 오류";return false;}totals[key]=sum;}
            foreach(var p in totals)if(!p.Key.Item1.InfiniteResources&&p.Key.Item1.Amount(p.Key.Item2)<p.Value){reason="소속 기지의 건설 자원이 부족합니다: "+p.Key.Item2;return false;}
            var before=new Dictionary<ResourceBank,(List<ResourceBank.Stock> stocks,bool infinite)>();
            foreach(var p in totals)if(!before.ContainsKey(p.Key.Item1))before.Add(p.Key.Item1,(p.Key.Item1.Capture(),p.Key.Item1.InfiniteResources));
            foreach(var p in totals)if(p.Key.Item1.Withdraw(p.Key.Item2,p.Value)!=p.Value){RestoreBefore();reason="건설 자원이 확정 전에 변경됐습니다.";return false;}
            try{if(commit())return true;reason="설치 검증에 실패했습니다. 비용은 차감되지 않았습니다.";}
            catch{RestoreBefore();throw;}
            RestoreBefore();return false;
            void RestoreBefore(){foreach(var pair in before){pair.Key.Restore(pair.Value.stocks);pair.Key.InfiniteResources=pair.Value.infinite;}}
        }
    }
}
