using System;
using System.Threading.Tasks;

namespace Firebase
{
    public static class TaskDelayProvider
    {
        public static Func<TimeSpan, Task> Constructor { get; set; } = Task.Delay;
    }
}
