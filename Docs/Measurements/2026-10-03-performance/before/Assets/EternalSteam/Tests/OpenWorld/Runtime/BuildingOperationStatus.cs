namespace EternalSteam.OpenWorld
{
    // Area operation and access to the original base's inventory are separate states.
    public static class BuildingOperationStatus
    {
        public static bool OwnerLost(BuildingInstance building,BaseRegistry bases)
        {
            if(building==null||bases==null||string.IsNullOrEmpty(building.OwnerBaseId))return false;
            return !bases.Bases.TryGetValue(building.OwnerBaseId,out var context)||!context.Active;
        }

        public static string Describe(BuildingInstance building,BaseRegistry bases)
        {
            if(building==null)return string.Empty;
            bool lost=OwnerLost(building,bases);
            if(building.Operational)
                return lost?"영역 내 가동 · 소속 기지 상실\n원소속 재고 이용 불가 · 생산/역 운송 중단":"가동 중";
            if(building.OperationBlock.HasFlag(OperationBlock.OutsideArea))
                return lost?"유효 범위 밖 · 비작동\n소속 기지 상실 · 원소속 재고 이용 불가":"유효 범위 밖 · 비작동";
            return building.OperationBlock.HasFlag(OperationBlock.BaseLost)||lost?"기지 없음 / 상실 · 비작동":"비작동";
        }
    }
}
