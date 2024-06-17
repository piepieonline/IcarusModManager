// Copyright 2022 Crystal Ferrai
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using IcarusModManager.Model;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Exceptions;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace IcarusModManager.Integrator
{
	/// <summary>
	/// Applies serialized patches to a serialized json object or array
	/// </summary>
	/// <remarks>
	/// Uses the JsonPatch spec for applying patches.
	/// Summary: https://jsonpatch.com/
	/// Spec: https://datatracker.ietf.org/doc/html/rfc6902
	/// Library: https://docs.microsoft.com/en-us/aspnet/core/web-api/jsonpatch
	/// </remarks>
	internal static class JsonIntegrator
	{
		/// <summary>
		/// Performs an integration
		/// </summary>
		/// <param name="source">The serialized source Json data to patch</param>
		/// <param name="patches">The patches to apply</param>
		/// <returns>The Json with the patches applied to it</returns>
		public static string Integrate(string source, IEnumerable<List<JsonPatchOperation>> patches)
		{
			object? sourceObj = JsonConvert.DeserializeObject(source);
			JObject jSourceObj = JObject.Parse(source);
			if (sourceObj == null) throw new ArgumentException("Unable to parse source.", nameof(source));

			DefaultContractResolver contractResolver = new();

			foreach (List<JsonPatchOperation> patchList in patches)
			{
				List<Operation> expandedPatchList = new List<Operation>();
				foreach (JsonPatchOperation op in patchList)
				{
					// Old style patch, uses JSONPointer in the path field
                    if (op.path != null)
					{
                        expandedPatchList.Add(new Operation(op.op, op.path, op.from, op.value));
                    }
					else if(op.query != null)
					{
						// Uses JSONQuery to select multiple rows, and JSON pointer to select specific subfields
						foreach(var newPath in jSourceObj.SelectTokens(op.query))
						{
							expandedPatchList.Add(new Operation(op.op, ConvertPathToPointer(newPath.Path) + op.pointer.TrimStart('@'), op.from, op.value));
						}
					}
					else
					{
						// Direct JSONPointer path
						expandedPatchList.Add(new Operation(op.op, op.pointer, op.from, op.value));
					}
				}

                JsonPatchDocument document = new JsonPatchDocument(expandedPatchList, contractResolver);
				try
				{
					document.ApplyTo(sourceObj);
				}
				catch (JsonPatchException)
				{
					// It is valid for patches to fail. We should still continue with further patches.
				}
			}

			return JsonConvert.SerializeObject(sourceObj, Formatting.Indented);
		}

		private static string ConvertPathToPointer(string path)
		{
			var pointer = Regex.Replace(path, @"\[(.+?)\]\.?", @"/$1/").Replace(".", "/").Replace("'", "");

			return "/" + pointer;

        }
	}
}
