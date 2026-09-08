using System;
using System.Collections;
using System.Diagnostics;
using UnityEditor;

namespace LevelDesign.Editor
{
    public sealed class EditorBatch : IDisposable
    {
        readonly IEnumerator routine;
        readonly Action changed;
        readonly Action<Exception> failed;
        public bool Running { get; private set; } = true;
        public EditorBatch(IEnumerator routine, Action changed, Action<Exception> failed)
        {
            this.routine = routine; this.changed = changed; this.failed = failed;
            EditorApplication.update += Tick;
        }
        void Tick()
        {
            var clock = Stopwatch.StartNew();
            try
            {
                do { if (!routine.MoveNext()) { Dispose(); break; } }
                while (clock.ElapsedMilliseconds < 8);
            }
            catch (Exception error) { failed(error); Dispose(); }
            changed();
        }
        public void Dispose()
        {
            if (!Running) return;
            Running = false;
            EditorApplication.update -= Tick;
            (routine as IDisposable)?.Dispose();
            changed();
        }
    }
}
