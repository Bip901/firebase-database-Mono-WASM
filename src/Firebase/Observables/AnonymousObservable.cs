using System;

namespace Firebase.Observables
{
    public class AnonymousObservable<T> : IObservable<T>
    {
        private readonly Func<IObserver<T>, IDisposable> subscribe;

        public AnonymousObservable(Func<IObserver<T>, IDisposable> subscribe)
        {
            this.subscribe = subscribe;
        }

        public static AnonymousObservable<T> Create(Func<IObserver<T>, IDisposable> subscribe)
        {
            return new AnonymousObservable<T>(subscribe);
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            return subscribe(observer);
        }
    }
}
