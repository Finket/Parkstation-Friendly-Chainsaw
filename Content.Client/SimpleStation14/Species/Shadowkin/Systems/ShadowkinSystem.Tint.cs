﻿using Robust.Client.Graphics;
using Robust.Client.Player;
using Content.Client.SimpleStation14.Overlays.Shaders;
using Content.Shared.SimpleStation14.Species.Shadowkin.Components;
using Robust.Client.GameObjects;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Robust.Shared.Player;

namespace Content.Client.SimpleStation14.Species.Shadowkin.Systems;

public sealed class ShadowkinTintSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IEntityManager _entity = default!;

    private ColorTintOverlay _tintOverlay = default!;


    public override void Initialize()
    {
        base.Initialize();

        _tintOverlay = new ColorTintOverlay
        {
            TintColor = new Vector3(0.5f, 0f, 0.5f),
            TintAmount = 0.25f,
            Comp = new ShadowkinComponent()
        };

        SubscribeLocalEvent<ShadowkinComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ShadowkinComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ShadowkinComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<ShadowkinComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }


    private void OnStartup(EntityUid uid, ShadowkinComponent component, ComponentStartup args)
    {
        if (_player.LocalPlayer?.ControlledEntity != uid)
            return;

        _overlay.AddOverlay(_tintOverlay);
    }

    private void OnShutdown(EntityUid uid, ShadowkinComponent component, ComponentShutdown args)
    {
        if (_player.LocalPlayer?.ControlledEntity != uid)
            return;

        _overlay.RemoveOverlay(_tintOverlay);
    }

    private void OnPlayerAttached(EntityUid uid, ShadowkinComponent component, LocalPlayerAttachedEvent args)
    {
        _overlay.AddOverlay(_tintOverlay);
    }

    private void OnPlayerDetached(EntityUid uid, ShadowkinComponent component, LocalPlayerDetachedEvent args)
    {
        _overlay.RemoveOverlay(_tintOverlay);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _overlay.RemoveOverlay(_tintOverlay);
    }


    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var uid = _player.LocalPlayer?.ControlledEntity;
        if (uid == null ||
            !_entity.TryGetComponent(uid, out ShadowkinComponent? comp) ||
            !_entity.TryGetComponent(uid, out SpriteComponent? sprite) ||
            !sprite.LayerMapTryGet(HumanoidVisualLayers.Eyes, out var index) ||
            !sprite.TryGetLayer(index, out var layer))
            return;

        // Eye color
        comp.TintColor = new Vector3(layer.Color.R, layer.Color.G, layer.Color.B);

        const float min = 0.45f;
        const float max = 0.75f;
        // TODO This math doesn't match the comments, figure out which is correct
        comp.TintIntensity = Math.Clamp(min + (comp.PowerLevel / comp.PowerLevelMax) / 3, min, max);

        UpdateShader(comp.TintColor, comp.TintIntensity);
    }


    private void UpdateShader(Vector3? color, float? intensity)
    {
        while (_overlay.HasOverlay<ColorTintOverlay>())
            _overlay.RemoveOverlay(_tintOverlay);

        if (color != null)
            _tintOverlay.TintColor = color;
        if (intensity != null)
            _tintOverlay.TintAmount = intensity;

        _overlay.AddOverlay(_tintOverlay);
    }
}
