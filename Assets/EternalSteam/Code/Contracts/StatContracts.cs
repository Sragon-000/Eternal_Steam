namespace EternalSteam
{
    public interface IPerformanceScaling
    {
        float Increase(float value);
        float Decrease(float value);
        int Count(int value);
    }
}
