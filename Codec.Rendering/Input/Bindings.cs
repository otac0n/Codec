// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Rendering.Input
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DevDecoder.HIDDevices;

    public class Bindings<T>
    {
        private readonly List<EachBinding> eachBindings = new();
        private readonly List<CurrentBinding> currentBindings = new();

        public IEnumerable<(long Timestamp, double Value, T Binding)> Bind(ControlChangeTracker tracker, ControlChange[] changes)
        {
            foreach (var c in changes)
            {
                if (double.IsNaN(c.Value))
                {
                    continue;
                }

                var matching = from eachBinding in this.eachBindings
                               from p in eachBinding.Bindings.Where(b => b.Predicate(c.Control)).Take(1)
                               select (eachBinding.Value, p.Converter);

                foreach (var (value, converter) in matching)
                {
                    if (converter(c.Value) && !converter(c.PreviousValue))
                    {
                        yield return (c.Timestamp, c.Value, value);
                    }
                }
            }

            foreach (var currentBinding in this.currentBindings)
            {
                var matching = from c in tracker.Controls
                               from p in currentBinding.Bindings.Where(b => b.Predicate(c)).Take(1)
                               select (Change: tracker[c], p.Converter);

                var lastTime = 0L;
                var lastValue = double.NaN;
                foreach (var (change, converter) in matching)
                {
                    if (double.IsNaN(change.Value))
                    {
                        if (double.IsNaN(lastValue))
                        {
                            lastTime = change.Timestamp;
                        }
                    }
                    else if (change.Timestamp > lastTime)
                    {
                        lastTime = change.Timestamp;
                        lastValue = converter(change.Value);
                    }
                }

                yield return (lastTime, lastValue, currentBinding.Value);
            }
        }

        public void BindCurrent((Func<Control, bool> predicate, Func<double, double> converter)[] bindings, T value)
        {
            this.currentBindings.Add(new CurrentBinding(bindings, value));
        }

        public void BindEach(Func<Control, bool>[] bindings, T value)
        {
            this.eachBindings.Add(new EachBinding(Array.ConvertAll(bindings, b => (b, new Func<double, bool>(v => v >= 2 / 3d))), value));
        }

        public void BindEach((Func<Control, bool> predicate, Func<double, bool> converter)[] bindings, T value)
        {
            this.eachBindings.Add(new EachBinding(bindings, value));
        }

        private record struct EachBinding((Func<Control, bool> Predicate, Func<double, bool> Converter)[] Bindings, T Value);

        private record struct CurrentBinding((Func<Control, bool> Predicate, Func<double, double> Converter)[] Bindings, T Value);
    }
}
