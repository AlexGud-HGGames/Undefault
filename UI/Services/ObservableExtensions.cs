using System;

namespace UI.Services;

public static class ObservableExtensions
{
    public static IDisposable Subscribe<T>(this IObservable<T> observable, Action<T> onNext)
    {
        if (observable is null)
        {
            throw new ArgumentNullException(nameof(observable));
        }

        if (onNext is null)
        {
            throw new ArgumentNullException(nameof(onNext));
        }

        return observable.Subscribe(new ActionObserver<T>(onNext));
    }

    private sealed class ActionObserver<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;

        public ActionObserver(Action<T> onNext)
        {
            _onNext = onNext;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(T value)
        {
            _onNext(value);
        }
    }
}
