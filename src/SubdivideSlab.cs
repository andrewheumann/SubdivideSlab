using Elements;
using Elements.Spatial;
using Elements.Geometry;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using Elements.Geometry.Solids;
using Newtonsoft.Json;


namespace SubdivideSlab
{
    public static class SubdivideSlab
    {
        /// <summary>
        /// The SubdivideSlab function.
        /// </summary>
        /// <param name="model">The input model.</param>
        /// <param name="input">The arguments to the execution.</param>
        /// <returns>A SubdivideSlabOutputs instance containing computed results and the model with any new elements.</returns>
        public static SubdivideSlabOutputs Execute(Dictionary<string, Model> inputModels, SubdivideSlabInputs input)
        {
            var allFloors = new List<Floor>();
            inputModels.TryGetValue("Floors", out var flrModel);
            if (flrModel == null || flrModel.AllElementsOfType<Floor>().Count() == 0)
            {
                throw new ArgumentException("No Floors found.");
            }
            allFloors.AddRange(flrModel.AllElementsOfType<Floor>());
            List<SlabSubdivision> subdivisions = new List<SlabSubdivision>();
            for (int i = 0; i < allFloors.Count; i++)
            {
                Floor floor = allFloors[i];
                var floorId = StringExtensions.NumberToString(i);
                var profile = floor.Profile;
                var perimeter = profile.Perimeter;
                var voids = profile.Voids;
                var elevation = floor.Elevation;
                var boundaries = new List<Polygon>();
                boundaries.Add(perimeter);
                if (voids != null) boundaries.AddRange(voids);
                Transform transform = null;
                if (input.AlignToLongestEdge)
                {
                    var longestEdge = perimeter.Segments().OrderByDescending(p => p.Length()).First();
                    var xAxis = (longestEdge.End - longestEdge.Start).Unitized();
                    transform = new Transform(Vector3.Origin, xAxis, Vector3.ZAxis, 0);
                    transform.Invert();
                }
                var grid = new Grid2d(boundaries, transform);
                if (input.SubdivideAtVoidCorners && voids.Count > 0)
                {
                    foreach (var voidCrv in voids)
                    {
                        grid.SplitAtPoints(voidCrv.Vertices);
                    }
                    foreach (var cell in grid.CellsFlat)
                    {
                        cell.U.DivideByApproximateLength(input.Length, EvenDivisionMode.RoundUp);
                        cell.V.DivideByApproximateLength(input.Width, EvenDivisionMode.RoundUp);
                    }
                }
                else
                {
                    grid.U.DivideByApproximateLength(input.Length, EvenDivisionMode.RoundUp);
                    grid.V.DivideByApproximateLength(input.Width, EvenDivisionMode.RoundUp);
                }

                var cells = grid.GetCells();

                for (int i1 = 0; i1 < cells.Count; i1++)
                {
                    var id = $"{floorId}-{i1:000}";
                    Grid2d cell = cells[i1];
                    var cellCrvs = cell.GetTrimmedCellGeometry();
                    var isTrimmed = cell.IsTrimmed();
                    if (cellCrvs != null && cellCrvs.Length > 0)
                    {
                        subdivisions.Add(CreateSlabSubdivision(id, cellCrvs, floor, isTrimmed));
                    }
                }
            }

            var output = new SubdivideSlabOutputs(subdivisions.Count);
            output.model.AddElements(subdivisions);
            return output;
        }

        private static ModelCurve ToModelCurve(Curve curve, double elevation)
        {
            if (curve == null) return null;
            return new ModelCurve(curve, null, new Transform(0, 0, elevation));
        }

        private static SlabSubdivision CreateSlabSubdivision(string ID, IList<Curve> boundaries, Floor floor, bool isTrimmed)
        {
            var outerBoundary = boundaries.First();
            var polygon = (Polygon)outerBoundary;
            var profile = new Profile(polygon);
            if (boundaries.Count > 1)
            {
                profile.Voids = new List<Polygon>();
                for (int i = 1; i < boundaries.Count; i++)
                {
                    profile.Voids.Add((Polygon)boundaries[i]);
                }
            }
            var depth = floor.Thickness;
            var transform = new Transform(0, 0, GetFloorElevation(floor) - depth);
            var extrude = new Extrude(profile, depth, new Vector3(0, 0, 1), false);
            var geomRep = new Representation(new[] { extrude });
            var material = BuiltInMaterials.Concrete;
            var volume = polygon.Area() * depth;
            return new SlabSubdivision(ID, profile, isTrimmed, depth, volume, transform, material, geomRep, false, Guid.NewGuid(), "");
        }
        private static double GetFloorElevation(Floor floor)
        {
            var profile = floor.ProfileTransformed();
            return profile.Perimeter.Vertices.First().Z + floor.Thickness + 0.1;

        }
    }

}