-- Z-City-inspired organism simulation for Lua.
-- C# still owns low-level BONELAB damage hooks; Lua owns portable state rules.

ZBC = ZBC or {}
ZBC.Medical = ZBC.Medical or {}

local Core = ZBC.Core

local function ensure()
    ZBC.Medical.State = ZBC.Medical.State or Core.NewOrganismState()
    return ZBC.Medical.State
end

function ZBC.Medical.UpdateFromSnapshot(snapshot)
    local state = ensure()
    for key, value in pairs(snapshot) do
        state[key] = value
    end

    ZBC.Medical.RecalculateDerivedState(state, 0.0)
end

function ZBC.Medical.ApplyDamageImpulse(part, damageType, damage, pain, bleedFactor)
    local state = ensure()
    local trauma = Core.Clamp(damage / 85.0, 0.0, 1.6)
    local painImpulse = Core.Clamp(pain / 75.0, 0.0, 1.8)

    state.pain = Core.Clamp(state.pain + painImpulse * 0.18, 0.0, 1.5)
    state.shock = Core.Clamp(state.shock + trauma * 0.14 + painImpulse * 0.10, 0.0, 1.5)
    state.bleedingRate = math.max(state.bleedingRate, bleedFactor * 20.0)

    if part == "Head" then
        state.consciousness = math.min(state.consciousness, 1.0 - trauma * 0.25)
    elseif part == "Torso" then
        state.shock = Core.Clamp(state.shock + trauma * 0.12, 0.0, 1.5)
    end
end

function ZBC.Medical.ApplyTreatment(itemType, part)
    local state = ensure()

    if itemType == "Bandage" then
        state.bleedingRate = math.max(0.0, state.bleedingRate - 12.0)
    elseif itemType == "Tourniquet" then
        state.bleedingRate = math.max(0.0, state.bleedingRate - 40.0)
        state.shock = math.max(0.0, state.shock - 0.05)
    elseif itemType == "BloodPack" then
        state.blood = Core.Clamp(state.blood + 0.22, 0.0, 1.0)
        state.shock = math.max(0.0, state.shock - 0.16)
    elseif itemType == "Splint" then
        state.fractures = math.max(0, state.fractures - 1)
        state.pain = math.max(0.0, state.pain - 0.10)
    elseif itemType == "Morphine" then
        state.analgesia = Core.Clamp(state.analgesia + 0.55, 0.0, 1.0)
    elseif itemType == "Adrenaline" or itemType == "ETGStimulator" or itemType == "SJ1Stimulator" then
        state.adrenaline = Core.Clamp(state.adrenaline + 0.7, 0.0, 1.0)
        state.consciousness = Core.Clamp(state.consciousness + 0.18, 0.0, 1.0)
    elseif itemType == "Painkillers" then
        state.analgesia = Core.Clamp(state.analgesia + 0.25, 0.0, 1.0)
    elseif itemType == "Medkit" then
        state.bleedingRate = math.max(0.0, state.bleedingRate - 18.0)
        state.pain = math.max(0.0, state.pain - 0.12)
    end
end

function ZBC.Medical.Tick(deltaTime)
    local state = ensure()
    ZBC.Blood.Tick(state, deltaTime)
    ZBC.Pain.Tick(state, deltaTime)
    ZBC.Shock.Tick(state, deltaTime)
    ZBC.Fractures.Tick(state, deltaTime)
    ZBC.Medical.RecalculateDerivedState(state, deltaTime)
    return state
end

function ZBC.Medical.RecalculateDerivedState(state, deltaTime)
    local bloodLoss = 1.0 - state.blood
    local oxygenLoss = 1.0 - state.oxygen
    local shockPressure = state.shock + bloodLoss * 0.45 + oxygenLoss * 0.35 + state.pain * 0.20

    state.pulse = Core.Clamp(70.0 + state.pain * 38.0 + state.shock * 44.0 + state.adrenaline * 55.0 - bloodLoss * 35.0, 0.0, 210.0)
    state.consciousness = Core.Clamp(1.0 - shockPressure * 0.55 - state.internalBleeding * 0.25, 0.0, 1.0)
    state.unconscious = state.consciousness < 0.18 and not state.dead
    state.recoilMultiplier = Core.Clamp(1.0 + state.pain * 0.35 + state.shock * 0.25 - state.analgesia * 0.20, 0.85, 1.9)
    state.legStrength = Core.Clamp(1.0 - state.shock * 0.25 - math.min(state.fractures, 2) * 0.25, 0.25, 1.0)
    state.meleeSpeed = Core.Clamp(1.0 - state.pain * 0.25 - state.shock * 0.20, 0.35, 1.0)
end
