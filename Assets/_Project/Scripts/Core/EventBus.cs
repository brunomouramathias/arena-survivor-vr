using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArenaSurvivor.Core
{
    // Barramento de eventos para desacoplar sistemas
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> handlers = new();

        public static void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (handlers.TryGetValue(type, out var existing))
                handlers[type] = Delegate.Combine(existing, handler);
            else
                handlers[type] = handler;
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (handlers.TryGetValue(type, out var existing))
            {
                var updated = Delegate.Remove(existing, handler);
                if (updated == null) handlers.Remove(type);
                else handlers[type] = updated;
            }
        }

        public static void Publish<T>(T eventData)
        {
            if (handlers.TryGetValue(typeof(T), out var handler))
                (handler as Action<T>)?.Invoke(eventData);
        }
    }

    // Eventos do jogo
    public struct EnemyDiedEvent  { public int Score; public Vector3 Position; }
    public struct PlayerDamagedEvent { public int CurrentHealth; public int MaxHealth; }
    public struct PlayerDiedEvent { }
    public struct WaveStartedEvent { public int WaveNumber; public bool IsBossWave; }
    public struct WaveClearedEvent { public int WaveNumber; public int BonusScore; }
    public struct WeaponChangedEvent { public int SlotIndex; }
    public struct ItemPickedUpEvent { public string ItemType; }
}
