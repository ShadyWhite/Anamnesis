﻿// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Actor.Utilities;
using Anamnesis.Dialogs;
using Anamnesis.GameData;
using Anamnesis.GameData.Excel;
using Anamnesis.GUI.Dialogs;
using Anamnesis.Memory;
using Anamnesis.Services;
using Anamnesis.Utils;
using Lumina.Excel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

public static class NpcAppearanceSearch
{
	public static void Search(ActorMemory actor)
	{
		Task.Factory.StartNew(() => SearchAsync(actor));
	}

	public static async Task SearchAsync(ActorMemory actor)
	{
		List<INpcBase> appearances = new();

		using (WaitDialog dlg = await WaitDialog.ShowAsync("Please Wait...", "NPC Appearance search"))
		{
			await dlg.SetProgress(0.1);
			Search(actor, GameDataService.BattleNPCs.Cast<INpcBase>(), ref appearances);
			await dlg.SetProgress(0.5);
			Search(actor, GameDataService.EventNPCs.Cast<INpcBase>(), ref appearances);
			////await dlg.SetProgress(0.8);
			////Search(actor, GameDataService.ResidentNPCs, ref appearances);

			await dlg.SetProgress(1.0);
		}

		if (appearances.Count <= 0)
		{
			GenericDialog.Show($"Appearance not found", "NPC Appearance search");
		}
		else
		{
			StringBuilder messageBuilder = new();
			messageBuilder.AppendLine($"Found {appearances.Count} appearances:");
			messageBuilder.AppendLine();

			StringBuilder jsonBuilder = new();
			foreach (INpcBase appearance in appearances)
			{
				if (appearance is BattleNpc)
				{
					jsonBuilder.Append("B:");
				}
				else if (appearance is EventNpc)
				{
					jsonBuilder.Append("E:");
				}
				else if (appearance is ResidentNpc)
				{
					jsonBuilder.Append("R:");
				}

				jsonBuilder.AppendLine(appearance.RowId.ToString("D7"));
			}

			messageBuilder.AppendLine(jsonBuilder.ToString());
			messageBuilder.AppendLine();
			messageBuilder.AppendLine("This has been copied to your clipboard.");

			await ClipboardUtility.CopyToClipboardAsync(jsonBuilder.ToString());

			await GenericDialog.ShowAsync(messageBuilder.ToString(), "NPC Appearance search", MessageBoxButton.OK);
		}
	}

	// TODO: This has no references. Remove/Fix?
	private static void SearchNames(string name, ref List<RawRow> results)
	{
		/* foreach (BattleNpcName battleNpcName in GameDataService.BattleNpcNames)
		{
			if (battleNpcName.Name.ToLower() == name.ToLower())
			{
				results.Add(battleNpcName);
			}
		}

		foreach (ResidentNpc residentNpc in GameDataService.ResidentNPCs)
		{
			if (residentNpc.Name.ToLower() == name.ToLower())
			{
				results.Add(residentNpc);
			}
		}

		foreach (EventNpc eventNpc in GameDataService.EventNPCs)
		{
			if (eventNpc.Name.ToLower() == name.ToLower())
			{
				results.Add(eventNpc);
			}
		} */
	}

	private static void Search(ActorMemory actor, IEnumerable<INpcBase> npcs, ref List<INpcBase> results)
	{
		foreach (INpcBase npc in npcs)
		{
			if (IsAppearance(actor, npc))
			{
				results.Add(npc);
			}
		}
	}

	private static bool IsAppearance(ActorMemory actor, INpcBase npc)
	{
		if (actor.ModelType != npc.ModelCharaRow)
			return false;

		if (actor.Customize != null)
		{
			if (!IsCustomize(actor.Customize, npc))
			{
				return false;
			}
		}

		if (actor.Equipment != null)
		{
			if (!IsEquipment(actor.Equipment, npc))
			{
				return false;
			}
		}

		return true;
	}

	// We dont check everything here, just enough to get unique.
	private static bool IsCustomize(ActorCustomizeMemory customize, INpcBase npc)
	{
		// It gets weird checking models other than players for race and tribe, so... try and skip
		if (npc.ModelCharaRow == 0)
		{
			if (customize.Race != npc.Race.Value.CustomizeRace ||
				customize.Tribe != npc.Tribe.Value.CustomizeTribe ||
				customize.Gender != (ActorCustomizeMemory.Genders)npc.Gender ||
				customize.Age != (ActorCustomizeMemory.Ages)npc.BodyType)
			{
				return false;
			}
		}

		if (customize.Skintone != npc.SkinColor ||
			customize.Eyes != npc.EyeShape ||
			customize.Head != npc.Face ||
			(int)customize.FacialFeatures != npc.FacialFeature ||
			npc.FacePaint != npc.FacePaint ||
			npc.HairStyle != npc.HairStyle ||
			customize.Mouth != npc.Mouth ||
			customize.Height != npc.Height)
		{
			return false;
		}

		return true;
	}

	private static bool IsEquipment(ActorEquipmentMemory equipment, INpcBase npc)
	{
		if (equipment.Head?.Is(npc.Head) == false)
			return false;

		if (equipment.Chest?.Is(npc.Body) == false)
			return false;

		if (equipment.Legs?.Is(npc.Legs) == false)
			return false;

		if (equipment.Feet?.Is(npc.Feet) == false)
			return false;

		if (equipment.Arms?.Is(npc.Hands) == false)
			return false;

		if (equipment.LFinger?.Is(npc.LeftRing) == false)
			return false;

		if (equipment.RFinger?.Is(npc.RightRing) == false)
			return false;

		if (equipment.Wrist?.Is(npc.Wrists) == false)
			return false;

		if (equipment.Neck?.Is(npc.Neck) == false)
			return false;

		if (equipment.Ear?.Is(npc.Ears) == false)
			return false;

		return true;
	}
}
