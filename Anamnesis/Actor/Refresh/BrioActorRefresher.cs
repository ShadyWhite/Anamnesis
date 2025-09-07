﻿// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Actor.Refresh;

using Anamnesis.Brio;
using Anamnesis.Core;
using Anamnesis.Files;
using Anamnesis.Memory;
using Anamnesis.Services;
using System.Threading.Tasks;
using XivToolsWpf;

public class BrioActorRefresher : IActorRefresher
{
	public bool CanRefresh(ActorMemory actor)
	{
		// Only if Brio integration is really enabled
		if (!SettingsService.Current.UseExternalRefreshBrio)
			return false;

		// Brio doesn't support refresh on world-frozen actors.
		// Trying to refresh a world-frozen actor will cause the actor to be
		// sent to world origin (0, 0, 0).
		if (PoseService.Instance.FreezeWorldPosition)
			return false;

		return true;
	}

	public async Task RefreshActor(ActorMemory actor)
	{
		await Dispatch.MainThread();

		bool doNpcHack = SettingsService.Current.EnableNpcHack && actor.ObjectKind == ActorTypes.Player;

		if (PoseService.Instance.IsEnabled)
		{
			// Save the current pose
			var poseFile = new PoseFile();
			var skeleton = new Skeleton(actor);
			poseFile.WriteToFile(actor, skeleton, null);

			// Redraw
			if (doNpcHack)
				actor.ObjectKind = ActorTypes.BattleNpc;

			try
			{
				var result = await Brio.Redraw(actor.ObjectIndex);

				if (result == "\"Full\"")
				{
					new Task(async () =>
					{
						await Task.Delay(500);
						await Dispatch.MainThread();

						// Restore current pose
						skeleton = new Skeleton(actor);
						poseFile.Apply(actor, skeleton, null, PoseFile.Mode.All, true);
					}).Start();
				}
			}
			finally
			{
				if (doNpcHack)
					actor.ObjectKind = ActorTypes.Player;
			}
		}
		else
		{
			// Outside of pose mode we can just refresh
			if (doNpcHack)
				actor.ObjectKind = ActorTypes.BattleNpc;

			try
			{
				await Brio.Redraw(actor.ObjectIndex);
			}
			finally
			{
				if (doNpcHack)
					actor.ObjectKind = ActorTypes.Player;
			}
		}
	}
}
