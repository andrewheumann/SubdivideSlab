using Elements;
using Elements.Spatial;
using Elements.Geometry;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using Elements.Geometry.Solids;


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
            var modelCurves = new List<ModelCurve>();
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
                var grid = new Grid2d(boundaries);
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
                List<ModelCurve> crvs = new List<ModelCurve>();

                for (int i1 = 0; i1 < cells.Count; i1++)
                {
                    var id = $"{floorId}-{i1:000}";
                    Grid2d cell = cells[i1];
                    var cellCrvs = cell.GetTrimmedCellGeometry();
                    if (cellCrvs != null && cellCrvs.Length > 0)
                    {
                        crvs.AddRange(cellCrvs.Select(cc => ToModelCurve(cc, GetFloorElevation(floor))));
                        subdivisions.Add(CreateSlabSubdivision(id, cellCrvs, floor));
                    }
                }
                modelCurves.AddRange(crvs);
            }

            var output = new SubdivideSlabOutputs(modelCurves.Count);
            output.model.AddElements(modelCurves);
            output.model.AddElements(subdivisions);
            return output;
        }

        private static ModelCurve ToModelCurve(Curve curve, double elevation)
        {
            if (curve == null) return null;
            return new ModelCurve(curve, null, new Transform(0, 0, elevation));
        }

        private static SlabSubdivision CreateSlabSubdivision(string ID, IList<Curve> boundaries, Floor floor)
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
            return new SlabSubdivision(ID, profile, depth, volume, transform, material, geomRep, Guid.NewGuid(), "");
        }
        private static double GetFloorElevation(Floor floor)
        {
            var profile = floor.ProfileTransformed();
            return profile.Perimeter.Vertices.First().Z + floor.Thickness + 0.1;

        }
    }

}