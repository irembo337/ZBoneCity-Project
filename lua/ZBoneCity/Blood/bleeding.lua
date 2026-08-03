ZBC = ZBC or {}
ZBC.Blood = ZBC.Blood or {}

function ZBC.Blood.Tick(state, deltaTime)
    if state.dead then return end

    local loss = math.max(0.0, state.bleedingRate) * deltaTime / 5000.0
    state.blood = ZBC.Core.Clamp(state.blood - loss, 0.0, 1.0)

    if state.bleedingRate <= 0.01 and state.blood < 1.0 and not state.unconscious then
        state.blood = ZBC.Core.Clamp(state.blood + deltaTime * 0.0005, 0.0, 1.0)
    end

    if state.internalBleeding > 0.01 then
        state.blood = ZBC.Core.Clamp(state.blood - state.internalBleeding * deltaTime * 0.0015, 0.0, 1.0)
    end
end
