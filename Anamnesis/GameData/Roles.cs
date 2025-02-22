﻿// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.GameData;

using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable SA1649

public enum Roles
{
	Tanks,
	Healers,
	Damage,
	Gatherers,
	Crafters,
}

public static class RolesExtensions
{
	private static Dictionary<Roles, List<Classes>>? classLookup;

	public static List<Classes> GetClasses(this Roles role)
	{
		if (classLookup == null)
		{
			classLookup = [];

			foreach (Classes? job in Enum.GetValues<Classes>().Select(v => (Classes?)v))
			{
				if (job == null || job == Classes.None)
					continue;

				Roles? classRole = ((Classes)job).GetRole();

				if (classRole == null)
					continue;

				if (!classLookup.ContainsKey((Roles)classRole))
					classLookup.Add((Roles)classRole, new List<Classes>());

				classLookup[(Roles)classRole].Add((Classes)job);
			}
		}

		return classLookup[role];
	}

	public static string GetName(this Roles role)
	{
		return role.ToString();
	}
}
