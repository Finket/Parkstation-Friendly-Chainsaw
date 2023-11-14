﻿using Content.Server.SimpleStation14.Species.Shadowkin.Components;
using Content.Shared.SimpleStation14.Species.Shadowkin.Events;

namespace Content.Server.SimpleStation14.Species.Shadowkin.Systems;

public sealed class ShadowkinBlackeyeTraitSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entity = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShadowkinBlackeyeTraitComponent, ComponentStartup>(OnStartup);
    }


    private void OnStartup(EntityUid uid, ShadowkinBlackeyeTraitComponent _, ComponentStartup args)
    {
        var ent = _entity.GetNetEntity(uid);
        RaiseLocalEvent(uid, new ShadowkinBlackeyeEvent(ent, false));
        RaiseNetworkEvent(new ShadowkinBlackeyeEvent(ent, false));
    }
}
