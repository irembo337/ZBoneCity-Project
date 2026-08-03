ZBC = ZBC or {}
ZBC.Shock = ZBC.Shock or {}

function ZBC.Shock.Tick(state, deltaTime)
    local bloodLoss = 1.0 - state.blood
    local target = bloodLoss * 0.75 + state.pain * 0.35 + state.internalBleeding * 0.45 + math.min(state.fractures, 3) * 0.10

    if target > state.shock then
        state.shock = ZBC.Core.MoveToward(state.shock, target, deltaTime * 0.08)
    else
        state.shock = ZBC.Core.MoveToward(state.shock, target, deltaTime * 0.025)
    end
end
