-- ZBoneCity Lua core.
-- Porting target for Z-City-style gameplay rules that do not need direct
-- Harmony/IL2CPP/BONELAB internals.

ZBC = ZBC or {}
ZBC.Core = ZBC.Core or {}

function ZBC.Core.Clamp(value, minValue, maxValue)
    if value < minValue then return minValue end
    if value > maxValue then return maxValue end
    return value
end

function ZBC.Core.MoveToward(current, target, amount)
    if current < target then
        return math.min(current + amount, target)
    end

    return math.max(current - amount, target)
end

function ZBC.Core.Remap(value, inMin, inMax, outMin, outMax)
    if math.abs(inMax - inMin) < 0.00001 then return outMin end
    local t = (value - inMin) / (inMax - inMin)
    return outMin + (outMax - outMin) * ZBC.Core.Clamp(t, 0.0, 1.0)
end

function ZBC.Core.NewOrganismState()
    return {
        blood = 1.0,
        oxygen = 1.0,
        pain = 0.0,
        shock = 0.0,
        fear = 0.0,
        analgesia = 0.0,
        adrenaline = 0.0,
        pulse = 70.0,
        consciousness = 1.0,
        bleedingRate = 0.0,
        internalBleeding = 0.0,
        infectionRisk = 0.0,
        fractures = 0,
        unconscious = false,
        dead = false,
        recoilMultiplier = 1.0,
        legStrength = 1.0,
        meleeSpeed = 1.0
    }
end
