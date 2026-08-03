ZBC = ZBC or {}
ZBC.HUD = ZBC.HUD or {}

function ZBC.HUD.BuildStatusModel(state)
    return {
        bloodPercent = math.floor((state.blood or 0.0) * 100.0 + 0.5),
        oxygenPercent = math.floor((state.oxygen or 0.0) * 100.0 + 0.5),
        painPercent = math.floor(math.min(state.pain or 0.0, 1.0) * 100.0 + 0.5),
        shockPercent = math.floor(math.min(state.shock or 0.0, 1.0) * 100.0 + 0.5),
        pulse = math.floor((state.pulse or 0.0) + 0.5),
        unconscious = state.unconscious == true,
        dead = state.dead == true
    }
end
