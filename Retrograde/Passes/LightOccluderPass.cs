using FrankyCLI.Retrograde.Passes;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.Retrograde
{
    //This is an pass only needed for ships/stations, it blocks space lights
    public class LightOccluderPass : IGenPass
    {
        private static RgAabb ComputeDungeonBounds(List<PlacedRoom> placedRooms)
        {
            if (placedRooms == null || placedRooms.Count == 0)
                throw new Exception("No placed rooms to bound.");

            // Start with first room bounds
            var first = placedRooms[0];
            var b0 = ToWorldAabbRotated(first.Prefab.packin_instance.ObjectBounds, first.WorldPos, first.YawSteps);

            float minX = b0.Min.X, minY = b0.Min.Y, minZ = b0.Min.Z;
            float maxX = b0.Max.X, maxY = b0.Max.Y, maxZ = b0.Max.Z;

            for (int i = 1; i < placedRooms.Count; i++)
            {
                var r = placedRooms[i];
                var b = ToWorldAabbRotated(r.Prefab.packin_instance.ObjectBounds, r.WorldPos, r.YawSteps);

                if (b.Min.X < minX) minX = b.Min.X;
                if (b.Min.Y < minY) minY = b.Min.Y;
                if (b.Min.Z < minZ) minZ = b.Min.Z;

                if (b.Max.X > maxX) maxX = b.Max.X;
                if (b.Max.Y > maxY) maxY = b.Max.Y;
                if (b.Max.Z > maxZ) maxZ = b.Max.Z;
            }

            return new RgAabb
            {
                Min = new P3Float(minX, minY, minZ),
                Max = new P3Float(maxX, maxY, maxZ)
            };
        }

        private static float SnapDown(float v, float step) => (float)Math.Floor(v / step) * step;
        private static float SnapUp(float v, float step) => (float)Math.Ceiling(v / step) * step;

        private static RgAabb ToWorldAabbRotated(ObjectBounds boundsLocal, P3Float worldPos, int yawSteps)
        {
            // Rotate 8 corners of the local AABB; then take min/max in world space.
            var min = boundsLocal.First;
            var max = boundsLocal.Second;

            P3Float[] corners =
            {
                new P3Float(min.X, min.Y, min.Z),
                new P3Float(min.X, min.Y, max.Z),
                new P3Float(min.X, max.Y, min.Z),
                new P3Float(min.X, max.Y, max.Z),
                new P3Float(max.X, min.Y, min.Z),
                new P3Float(max.X, min.Y, max.Z),
                new P3Float(max.X, max.Y, min.Z),
                new P3Float(max.X, max.Y, max.Z),
            };

            var first = worldPos + RgRotation.RotateYaw90(corners[0], yawSteps);
            float minX = first.X, minY = first.Y, minZ = first.Z;
            float maxX = first.X, maxY = first.Y, maxZ = first.Z;

            for (int i = 1; i < corners.Length; i++)
            {
                var w = worldPos + RgRotation.RotateYaw90(corners[i], yawSteps);
                if (w.X < minX) minX = w.X;
                if (w.Y < minY) minY = w.Y;
                if (w.Z < minZ) minZ = w.Z;
                if (w.X > maxX) maxX = w.X;
                if (w.Y > maxY) maxY = w.Y;
                if (w.Z > maxZ) maxZ = w.Z;
            }

            return new RgAabb
            {
                Min = new P3Float(minX, minY, minZ),
                Max = new P3Float(maxX, maxY, maxZ),
            };
        }

        public void RunPass(DungeonState state)
        {
            string panelPackInEditorId = "rg_panel_8x4_lightoccluder";
            float padding = 48f;     // “space around all the rooms”
            float panelW = 8f;
            float panelH = 4f;
            float surfaceOffset = 0.5f; // small push outward to avoid z-fighting

            // 1) Compute and expand bounds
            var bounds = ComputeDungeonBounds(state.placedRooms);

            float roofsquish = 6;

            var min = new P3Float(bounds.Min.X - padding, bounds.Min.Y - padding, bounds.Min.Z - padding);
            var max = new P3Float(bounds.Max.X + padding, bounds.Max.Y + padding, bounds.Max.Z + padding);

            // 2) Snap to panel grid to avoid gaps (intentionally grows)
            // X span uses panelW; Y span uses panelW on walls but panelH on floor. We snap conservatively:
            min = new P3Float(SnapDown(min.X, panelW), SnapDown(min.Y, panelW), SnapDown(min.Z, panelH));
            max = new P3Float(SnapUp(max.X, panelW), SnapUp(max.Y, panelW), SnapUp(max.Z, panelH));

            // 3) Place faces
            var panelPrefab = new RoomPrefab(panelPackInEditorId);

            // Walls at X = min.X and max.X (grid in Y/Z)
            PlaceWall_X(state.instance, panelPrefab, min.X - surfaceOffset, min.Y, max.Y, min.Z, max.Z, panelW, panelH, yawSteps: 1); // facing +X
            PlaceWall_X(state.instance, panelPrefab, max.X + surfaceOffset, min.Y, max.Y, min.Z, max.Z, panelW, panelH, yawSteps: 3); // facing -X

            // Walls at Y = min.Y and max.Y (grid in X/Z)
            PlaceWall_Y(state.instance, panelPrefab, min.Y - surfaceOffset, min.X, max.X, min.Z, max.Z, panelW, panelH, yawSteps: 0); // facing +Y (adjust if needed)
            PlaceWall_Y(state.instance, panelPrefab, max.Y + surfaceOffset, min.X, max.X, min.Z, max.Z, panelW, panelH, yawSteps: 2); // facing -Y

            // Floor/Ceiling at Z = min.Z and max.Z (grid in X/Y)
            PlaceCap_Z(state.instance, panelPrefab, min.Z - surfaceOffset + roofsquish, min.X, max.X, min.Y, max.Y, panelW, panelH, pitch: 90f);  // floor (panel facing up)
            PlaceCap_Z(state.instance, panelPrefab, max.Z + surfaceOffset - roofsquish, min.X, max.X, min.Y, max.Y, panelW, panelH, pitch: 270f); // ceiling (panel facing down)
        }

        private void PlaceWall_X(
            Cell cell,
            RoomPrefab panelPrefab,
            float x,
            float yMin, float yMax,
            float zMin, float zMax,
            float stepY, float stepZ,
            int yawSteps
        )
        {
            float yStart = yMin + stepY * 0.5f;
            float zStart = zMin + stepZ * 0.5f;

            for (float y = yStart; y <= yMax - stepY * 0.5f; y += stepY)
            {
                for (float z = zStart; z <= zMax - stepZ * 0.5f; z += stepZ)
                {
                    cell.Temporary.Add(new PlacedObject(gen_quest_main.myMod)
                    {
                        Count = 1,
                        Rotation = RgRotation.RotationToP3Float(yawSteps),
                        Position = new P3Float(x, y, z),
                        Base = panelPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                    });
                }
            }
        }

        private void PlaceWall_Y(
            Cell cell,
            RoomPrefab panelPrefab,
            float y,
            float xMin, float xMax,
            float zMin, float zMax,
            float stepX, float stepZ,
            int yawSteps
        )
        {
            float xStart = xMin + stepX * 0.5f;
            float zStart = zMin + stepZ * 0.5f;

            for (float x = xStart; x <= xMax - stepX * 0.5f; x += stepX)
            {
                for (float z = zStart; z <= zMax - stepZ * 0.5f; z += stepZ)
                {
                    cell.Temporary.Add(new PlacedObject(gen_quest_main.myMod)
                    {
                        Count = 1,
                        Rotation = RgRotation.RotationToP3Float(yawSteps),
                        Position = new P3Float(x, y, z),
                        Base = panelPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                    });
                }
            }
        }

        private void PlaceCap_Z(
            Cell cell,
            RoomPrefab panelPrefab,
            float z,
            float xMin, float xMax,
            float yMin, float yMax,
            float stepX, float stepY,
            float pitch
        )
        {
            float xStart = xMin + stepX * 0.5f;
            float yStart = yMin + stepY * 0.5f;

            for (float x = xStart; x <= xMax - stepX * 0.5f; x += stepX)
            {
                for (float y = yStart; y <= yMax - stepY * 0.5f; y += stepY)
                {
                    cell.Temporary.Add(new PlacedObject(gen_quest_main.myMod)
                    {
                        Count = 1,
                        Rotation = new P3Float(RgRotation.EulerToRadCardinals((int)pitch), 0f, 0f), // pitch-only; adjust axis if your Euler differs
                        Position = new P3Float(x, y, z),
                        Base = panelPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                    });
                }
            }
        }
    }
}
