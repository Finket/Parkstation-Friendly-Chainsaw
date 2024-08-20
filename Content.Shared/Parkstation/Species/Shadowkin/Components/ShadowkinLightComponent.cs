﻿using Robust.Shared.GameStates;

namespace Content.Shared.Parkstation.Species.Shadowkin.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ShadowkinLightComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid AttachedEntity = EntityUid.Invalid;


    [ViewVariables(VVAccess.ReadOnly)]
    public float OldRadius = 0f;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool OldRadiusEdited = false;


    [ViewVariables(VVAccess.ReadOnly)]
    public float OldEnergy = 0f;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool OldEnergyEdited = false;
}