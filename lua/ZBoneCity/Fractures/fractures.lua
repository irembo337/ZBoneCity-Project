ZBC = ZBC or {}
ZBC.Fractures = ZBC.Fractures or {}

function ZBC.Fractures.Tick(state, deltaTime)
    if state.fractures <= 0 then return end

    state.pain = ZBC.Core.Clamp(state.pain + deltaTime * 0.0025 * state.fractures, 0.0, 1.5)
    state.shock = ZBC.Core.Clamp(state.shock + deltaTime * 0.0015 * state.fractures, 0.0, 1.5)
end
