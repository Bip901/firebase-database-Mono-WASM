using System;

namespace Firebase.Observables
{
    /// <summary>
    /// Class to create an <see cref="IObserver{T}"/> instance from delegate-based implementations of the On* methods.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    public sealed class AnonymousObserver<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;
        private readonly Action<Exception> _onError;
        private readonly Action _onCompleted;

        /// <summary>
        /// Creates an observer from the specified <see cref="IObserver{T}.OnNext(T)"/>, <see cref="IObserver{T}.OnError(Exception)"/>, and <see cref="IObserver{T}.OnCompleted()"/> actions.
        /// </summary>
        /// <param name="onNext">Observer's <see cref="IObserver{T}.OnNext(T)"/> action implementation.</param>
        /// <param name="onError">Observer's <see cref="IObserver{T}.OnError(Exception)"/> action implementation.</param>
        /// <param name="onCompleted">Observer's <see cref="IObserver{T}.OnCompleted()"/> action implementation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="onNext"/> or <paramref name="onError"/> or <paramref name="onCompleted"/> is <c>null</c>.</exception>
        public AnonymousObserver(Action<T> onNext, Action<Exception> onError, Action onCompleted)
        {
            _onNext = onNext ?? throw new ArgumentNullException(nameof(onNext));
            _onError = onError ?? throw new ArgumentNullException(nameof(onError));
            _onCompleted = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));
        }

        /// <summary>
        /// Creates an observer from the specified <see cref="IObserver{T}.OnNext(T)"/> action.
        /// </summary>
        /// <param name="onNext">Observer's <see cref="IObserver{T}.OnNext(T)"/> action implementation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="onNext"/> is <c>null</c>.</exception>
        public AnonymousObserver(Action<T> onNext)
            : this(onNext, Throw, Nop)
        { }

        /// <summary>
        /// Creates an observer from the specified <see cref="IObserver{T}.OnNext(T)"/> and <see cref="IObserver{T}.OnError(Exception)"/> actions.
        /// </summary>
        /// <param name="onNext">Observer's <see cref="IObserver{T}.OnNext(T)"/> action implementation.</param>
        /// <param name="onError">Observer's <see cref="IObserver{T}.OnError(Exception)"/> action implementation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="onNext"/> or <paramref name="onError"/> is <c>null</c>.</exception>
        public AnonymousObserver(Action<T> onNext, Action<Exception> onError)
            : this(onNext, onError, Nop)
        { }

        /// <summary>
        /// Creates an observer from the specified <see cref="IObserver{T}.OnNext(T)"/> and <see cref="IObserver{T}.OnCompleted()"/> actions.
        /// </summary>
        /// <param name="onNext">Observer's <see cref="IObserver{T}.OnNext(T)"/> action implementation.</param>
        /// <param name="onCompleted">Observer's <see cref="IObserver{T}.OnCompleted()"/> action implementation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="onNext"/> or <paramref name="onCompleted"/> is <c>null</c>.</exception>
        public AnonymousObserver(Action<T> onNext, Action onCompleted)
            : this(onNext, Throw, onCompleted)
        { }

        public void OnCompleted()
        {
            _onCompleted();
        }

        public void OnError(Exception error)
        {
            _onError(error);
        }

        public void OnNext(T value)
        {
            _onNext(value);
        }

        private static void Nop()
        { }

        private static void Throw(Exception error)
        {
            throw error;
        }
    }
}
