using System;
using System.Collections.Generic;
using System.Linq;

namespace UI.Services;

public sealed class SimpleSubject<T> : IObservable<T>
{
    private readonly object _lock = new();
    private readonly List<IObserver<T>> _observers = new();

    public IDisposable Subscribe(IObserver<T> observer)
    {
        if (observer is null)
        {
            throw new ArgumentNullException(nameof(observer));
        }

        lock (_lock)
        {
            _observers.Add(observer);
        }

        return new Unsubscriber(_observers, _lock, observer);
    }

    public void OnNext(T value)
    {
        List<IObserver<T>> observers;

        lock (_lock)
        {
            observers = _observers.ToList();
        }

        foreach (var observer in observers)
        {
            observer.OnNext(value);
        }
    }

    private sealed class Unsubscriber : IDisposable
    {
        private readonly List<IObserver<T>> _observers;
        private readonly object _lock;
        private IObserver<T>? _observer;

        public Unsubscriber(List<IObserver<T>> observers, object lockObject, IObserver<T> observer)
        {
            _observers = observers;
            _lock = lockObject;
            _observer = observer;
        }

        public void Dispose()
        {
            if (_observer is null)
            {
                return;
            }

            lock (_lock)
            {
                _observers.Remove(_observer);
            }

            _observer = null;
        }
    }
}
