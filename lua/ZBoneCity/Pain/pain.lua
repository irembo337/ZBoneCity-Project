ZBC = ZBC or {}
ZBC.Pain = ZBC.Pain or {}

function ZBC.Pain.Tick(state, deltaTime)
    local recovery = deltaTime * (0.012 + state.analgesia * 0.045 + state.adrenaline * 0.012)
    state.pain = ZBC.Core.Clamp(state.pain - recovery, 0.0, 1.5)
    state.analgesia = ZBC.Core.MoveToward(state.analgesia, 0.0, deltaTime / 240.0)
    state.adrenaline = ZBC.Core.MoveToward(state.adrenaline, 0.0, deltaTime / 45.0)
end
