namespace EternalSteam
{
    public enum WeaponDamageSource { Normal, PlasmaLaser }
    public enum WeaponStatus { None, Overheat, Burn, Stun, Slow }
    public interface IWeaponDamageReceiver : IDamageReceiver
    {
        bool CanAct {get;}
        void Receive(float damage,WeaponDamageSource source);
        void Inflict(WeaponStatus status,float duration,float strength,float tickDamage);
    }
}
