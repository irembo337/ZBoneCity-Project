namespace BonelabAdvancedHealth
{
    public sealed class CasualtyDragSystem
    {
        private HealthManager? _draggedManager;
        private float _syncTimer;

        public bool IsDragging => _draggedManager != null;

        public void Reset()
        {
            _draggedManager = null;
            _syncTimer = 0f;
        }

        public bool CanDrag(HealthManager manager)
        {
            return Config.CasualtyDraggingEnabled &&
                   manager.Kind != HealthOwnerKind.Player &&
                   !manager.IsDead &&
                   (manager.Consciousness.State == ConsciousnessState.Unconscious || manager.Coma.IsActive);
        }

        public bool TryBeginDrag(HealthManager manager)
        {
            if (!CanDrag(manager))
                return false;

            _draggedManager = manager;
            _syncTimer = 0f;
            MainMod.Runtime?.NotifyFusionCasualtyDrag(manager, true);
            return true;
        }

        public void EndDrag()
        {
            HealthManager? manager = _draggedManager;
            _draggedManager = null;
            if (manager != null)
                MainMod.Runtime?.NotifyFusionCasualtyDrag(manager, false);
        }

        public void Update(float deltaTime)
        {
            if (_draggedManager == null)
                return;

            if (!CanDrag(_draggedManager))
            {
                EndDrag();
                return;
            }

            _syncTimer += deltaTime;
            if (_syncTimer >= 0.5f)
            {
                _syncTimer = 0f;
                MainMod.Runtime?.NotifyFusionCasualtyDrag(_draggedManager, true);
            }
        }
    }
}
