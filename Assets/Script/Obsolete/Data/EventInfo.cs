namespace YARG.Data
{
    public class EventInfo : AbstractInfo
    {
        public string name;

        public EventInfo(string name, float time, float length = 0f)
        {
            this.time = time;
            this.name = name;
            this.length = length;
        }
    }
}