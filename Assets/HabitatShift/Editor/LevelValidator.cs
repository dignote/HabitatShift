using System;
using HabitatShift.Core;
using UnityEditor;
using UnityEngine;
namespace HabitatShift.Editor {
public static class LevelValidator {
 [MenuItem("Habitat Shift/Validate Production Catalog v4")]
 public static void Validate(){try{RulesetDto rules;var catalog=CatalogLoader.Load(out rules);CatalogLoader.Validate(catalog,rules);Debug.Log("Habitat Shift catalog v4 valid: 18 levels, continuous-core-v2.");}catch(Exception e){Debug.LogError("Habitat Shift catalog validation failed: "+e.Message);}}
}
}
