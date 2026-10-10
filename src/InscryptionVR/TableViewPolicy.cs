namespace InscryptionVR
{
    internal enum TableViewMode { Seated, MapOverview, TopDown }
    internal sealed class TableViewPolicy
    {
        private bool _map;
        private bool _initialized;
        private TableViewMode? _manual;
        internal TableViewMode Current { get; private set; }
        internal bool Update(bool map, bool automatic, TableViewMode? requested)
        {
            if (!_initialized || map != _map) _manual = null;
            _map = map; _initialized = true;
            if (requested.HasValue) _manual = requested;
            TableViewMode next = _manual ?? (map && automatic ? TableViewMode.MapOverview : TableViewMode.Seated);
            bool changed = next != Current;
            Current = next;
            return changed;
        }
    }
}
