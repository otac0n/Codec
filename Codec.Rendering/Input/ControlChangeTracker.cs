// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Rendering.Input
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Reactive.Linq;
    using DevDecoder.HIDDevices;
    using DynamicData;

    public class ControlChangeTracker : IDisposable
    {
        private readonly Queue<ControlChange> queue = new();
        private readonly IDisposable subscription;
        private readonly Dictionary<Control, ControlChange> latest = [];

        public ControlChangeTracker(Devices devices)
        {
            this.subscription = devices
                .Connect()
                .Flatten()
                .Select(change => change.Current)
                .SelectMany(d => d.Catch((Exception _) => Observable.Empty<IList<ControlChange>>()))
                .Where(l => l.Count > 0)
                .Subscribe(changeSet =>
                {
                    lock (this.queue)
                    {
                        for (var c = 0; c < changeSet.Count; c++)
                        {
                            var change = changeSet[c];
                            this.queue.Enqueue(change);
                        }
                    }
                });
        }

        public IEnumerable<Control> Controls => this.latest.Keys;

        public IEnumerable<ControlChange> Values => this.latest.Values;

        public ControlChange this[Control control] => this.latest[control];

        public void Dispose()
        {
            this.subscription.Dispose();
            GC.SuppressFinalize(this);
        }

        public ControlChange[] ProcessChanges()
        {
            ControlChange[] list;
            lock (this.queue)
            {
                list = [.. this.queue];
                this.queue.Clear();
            }

            Array.Sort(list, (a, b) => a.Timestamp.CompareTo(b.Timestamp));

            var toRemove = new HashSet<Control>();
            toRemove.UnionWith(this.latest.Keys.Where(k => !k.Device.IsConnected));

            for (var i = 0; i < list.Length; i++)
            {
                var value = list[i];
                var control = value.Control;
                if (toRemove.Remove(control))
                {
                    toRemove.ExceptWith(control.Device.Controls);
                }

                if (!this.latest.TryGetValue(control, out var existing) || existing.Timestamp < value.Timestamp)
                {
                    this.latest[control] = value;
                }
            }

            foreach (var r in toRemove)
            {
                this.latest.Remove(r);
            }

            return list;
        }

        public void ProcessChanges<T>(Bindings<Action<T>> bindings)
            where T : IFloatingPoint<T>
        {
            foreach (var (_, value, binding) in this.ProcessChanges<Action<T>>(bindings))
            {
                if (!double.IsNaN(value))
                {
                    binding(T.CreateChecked(value));
                }
            }
        }

        public IEnumerable<(long Timestamp, double Value, T Binding)> ProcessChanges<T>(Bindings<T> bindings)
        {
            var changes = this.ProcessChanges();
            return bindings.Bind(this, changes);
        }
    }
}
