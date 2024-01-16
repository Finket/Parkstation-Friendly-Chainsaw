﻿using Content.Server.Chat.Systems;
using Content.Server.CosmaticDrift.Storage.Components;
using Content.Server.EUI;
using Content.Server.Forensics;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Server.Storage.Components;
using Content.Server.Storage.EntitySystems;
using Content.Server.Warps;
using Content.Shared.ActionBlocker;
using Content.Shared.Climbing.Systems;
using Content.Shared.Destructible;
using Content.Shared.DragDrop;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.PDA;
using Content.Shared.Roles.Jobs;
using Content.Shared.StationRecords;
using Content.Shared.Verbs;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Enums;

namespace Content.Server.CosmaticDrift.CryoSleep;

public sealed class CryoSleepSystem : EntitySystem
{
    [Dependency] private readonly EuiManager _euiManager = null!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly ClimbSystem _climb = default!;
    [Dependency] private readonly StationJobsSystem _stationJobsSystem = default!;
    [Dependency] private readonly SharedJobSystem _sharedJobSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CryoSleepComponent, ComponentInit>(ComponentInit);
        SubscribeLocalEvent<CryoSleepComponent, GetVerbsEvent<AlternativeVerb>>(AddAlternativeVerbs);
        SubscribeLocalEvent<CryoSleepComponent, DestructionEventArgs>((e, c, _) => EjectBody(e, c));
        SubscribeLocalEvent<CryoSleepComponent, DragDropTargetEvent>(OnDragDrop);
    }

    private void ComponentInit(EntityUid uid, CryoSleepComponent component, ComponentInit args)
    {
        component.BodyContainer = _container.EnsureContainer<ContainerSlot>(uid, "body_container");
    }

    private bool InsertBody(EntityUid uid, EntityUid? toInsert, CryoSleepComponent component)
    {
        if (toInsert == null || IsOccupied(component))
            return false;

        if (!HasComp<MobStateComponent>(toInsert.Value))
            return false;

        var inserted = component.BodyContainer.Insert(toInsert.Value, EntityManager);

        return inserted;
    }

    public bool RespawnUser(EntityUid? toInsert, CryoSleepComponent component, bool force)
    {
        if (toInsert == null)
            return false;

        if (IsOccupied(component) && !force)
            return false;

        if (_mindSystem.TryGetMind(toInsert.Value, out var mind, out var mindComp))
        {
            var session = mindComp.Session;
            if (session != null && session.Status == SessionStatus.Disconnected)
            {
                InsertBody(toInsert.Value, component.Owner, component);
                return true;
            }
        }

        var success = component.BodyContainer.Insert(toInsert.Value, EntityManager);

        if (success && mindComp?.Session != null)
        {
            _euiManager.OpenEui(new CryoSleepEui(mind, this), mindComp.Session);
        }

        return success;
    }
    public void CryoStoreBody(EntityUid mindId)
    {
        if (!_sharedJobSystem.MindTryGetJob(mindId, out _, out var prototype))
            return;

        if (!TryComp<MindComponent>(mindId, out var mind))
            return;

        var body = mind.CurrentEntity;
        var job = prototype;

        var name = mind.CharacterName;

        if (body == null)
            return;

        // Remove the record. Hopefully.
        foreach (var item in _inventory.GetHandOrInventoryEntities(body.Value))
        {
            if (TryComp(item, out PdaComponent? pda) && TryComp(pda.ContainedId, out StationRecordKeyStorageComponent? keyStorage) && keyStorage.Key is { } key && _stationRecords.TryGetRecord(key.OriginStation, key, out GeneralStationRecord? record))
            {
                if (TryComp(body, out DnaComponent? dna) &&
                    dna.DNA != record.DNA)
                {
                    continue;
                }

                if (TryComp(body, out FingerprintComponent? fingerPrint) &&
                    fingerPrint.Fingerprint != record.Fingerprint)
                {
                    continue;
                }

                _stationRecords.RemoveRecord(key.OriginStation, key);
                Del(item);
            }
        }

        // Move their items
        MoveItems(body.Value);

        _gameTicker.OnGhostAttempt(mindId, false, true, mind: mind);
        EntityManager.DeleteEntity(body);

        // This is awful, it feels awful, and I hate it. Warp points are really the only thing I can confirm exists on every station, though CC might cause issues so ideally I'd use one with a var set
        var query = EntityQueryEnumerator<WarpPointComponent>();
        query.MoveNext(out var warpPoint, out var comp);

        // sets job slot
        var xform = Transform(warpPoint);

        if (!xform.GridUid.HasValue)
            return;

        var station = _stationSystem.GetOwningStation(xform.GridUid.Value);
        if (!station.HasValue)
           return;

        if (job == null)
            return;

        _stationJobsSystem.TryGetJobSlot(station.Value, job, out var amount);
        if (!amount.HasValue)
            return;

        _stationJobsSystem.TrySetJobSlot(station.Value, job, (int) amount.Value + 1, true);

        _chatSystem.DispatchStationAnnouncement(station.Value, Loc.GetString("cryo-leave-announcement", ("character", name!), ("job", job.LocalizedName)), "Cryo Pod", false);
    }

    private bool EjectBody(EntityUid pod, CryoSleepComponent component)
    {
        if (!IsOccupied(component))
            return false;

        var toEject = component.BodyContainer.ContainedEntity;
        if (toEject == null)
            return false;

        component.BodyContainer.Remove(toEject.Value);
        _climb.ForciblySetClimbing(toEject.Value, pod);

        return true;
    }

    private bool IsOccupied(CryoSleepComponent component)
    {
        return component.BodyContainer.ContainedEntity != null;
    }

    private void AddAlternativeVerbs(EntityUid uid, CryoSleepComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // Insert self verb
        if (!IsOccupied(component) &&
            _actionBlocker.CanMove(args.User))
        {
            AlternativeVerb verb = new()
            {
                Act = () => RespawnUser(args.User, component, false),
                Category = VerbCategory.Insert,
                Text = Loc.GetString("medical-scanner-verb-enter")
            };
            args.Verbs.Add(verb);
        }

        // Eject somebody verb
        if (IsOccupied(component))
        {
            AlternativeVerb verb = new()
            {
                Act = () => EjectBody(component.Owner, component),
                Category = VerbCategory.Eject,
                Text = Loc.GetString("medical-scanner-verb-noun-occupant")
            };
            args.Verbs.Add(verb);
        }
    }

    private void OnDragDrop(EntityUid uid, CryoSleepComponent component, ref DragDropTargetEvent args)
    {
        if (args.Handled || args.User != args.Dragged)
            return;

        RespawnUser(args.User, component, false);
    }

    private void MoveItems(EntityUid uid)
    {
        //// Make sure the entity being cryo'd has an inventory
        //if (!HasComp<EntityStorageComponent>(uid))
        //    return;

        // Get the locker
        var query = EntityQueryEnumerator<LostAndFoundComponent>();
        query.MoveNext(out var locker, out var lostAndFoundComponent);

        //// Make sure the locker exists and has storage
        //if (!locker.Valid || !TryComp<EntityStorageComponent>(uid, out var lockerStorageComp))
        //    return;
        TryComp<EntityStorageComponent>(uid, out var lockerStorageComp);

        var coordinates = Transform(locker).Coordinates;

        // Go through their inventory and put everything in al ocker
        foreach (var item in _inventory.GetHandOrInventoryEntities(uid))
        {
            if (!item.IsValid() || !TryComp<MetaDataComponent>(item, out var comp))
                continue;

            var proto = comp.EntityPrototype;
            var ent = EntityManager.SpawnEntity(proto!.ID, coordinates);

            _entityStorage.Insert(ent, locker, lockerStorageComp);
        }
    }
}
