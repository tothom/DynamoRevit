using Dynamo.Scheduler;

namespace Dynamo.Applications
{
    public class RDASchedulerThread : ISchedulerThread
    {
        private IScheduler scheduler;

        public RDASchedulerThread() { }

        public void Initialize(IScheduler owningScheduler)
        {
            scheduler = owningScheduler;
        }

        public void Shutdown() { }

        private void Run()
        {
            const bool waitIfTaskQueueIsEmpty = false;
            while (scheduler.ProcessNextTask(waitIfTaskQueueIsEmpty))
            {
                // Does nothing here, loop ends when all tasks processed.
            }
        }
    }
}
