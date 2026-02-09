using CombatMechanix.Models;

namespace CombatMechanix.Services
{
    /// <summary>
    /// Server-authoritative terrain system with dynamic hill set management
    /// The server is the source of truth for terrain, clients fetch and recreate terrain from server data
    /// </summary>
    public class TerrainService
    {
        private readonly ILogger<TerrainService> _logger;

        // Dynamic hill sets - can be loaded/unloaded at runtime
        private readonly Dictionary<string, List<HillData>> _hillSets;
        private readonly List<string> _activeHillSets;
        private List<HillData> _cachedActiveHills;

        public TerrainService(ILogger<TerrainService> logger)
        {
            _logger = logger;
            _hillSets = new Dictionary<string, List<HillData>>();
            _activeHillSets = new List<string>();
            _cachedActiveHills = new List<HillData>();

            // Initialize with default hill sets
            InitializeDefaultHillSets();
            LoadHillSet("default");

            _logger.LogInformation($"TerrainService initialized with {_hillSets.Count} hill sets, {_cachedActiveHills.Count} active hills");
        }

        /// <summary>
        /// Get all active terrain data for client synchronization
        /// </summary>
        public ServerTerrainData GetTerrainData()
        {
            return new ServerTerrainData
            {
                BaseGroundLevel = 0f,
                Hills = _cachedActiveHills.Select(h => new TerrainHill
                {
                    Id = h.Id,
                    Name = h.Name,
                    Position = h.Position,
                    Scale = h.Scale,
                    Color = h.Color,
                    HillSet = h.HillSet
                }).ToList(),
                ActiveHillSets = _activeHillSets.ToList()
            };
        }

        /// <summary>
        /// Get the ground height at a specific X,Z position
        /// </summary>
        public float GetGroundHeightAtPosition(float x, float z)
        {
            float baseGroundHeight = 0f; // Base ground plane is at y=0
            float maxHillHeight = baseGroundHeight;

            // Check each active hill to see if this position is on a hill
            foreach (var hill in _cachedActiveHills)
            {
                float hillHeight = CalculateHillHeightAtPosition(hill, x, z);
                if (hillHeight > maxHillHeight)
                {
                    maxHillHeight = hillHeight;
                }
            }

            return maxHillHeight;
        }

        /// <summary>
        /// Check if a position is clear of terrain hills (walkable)
        /// </summary>
        public bool IsPositionClear(float x, float z, float maxAllowedHeight = 0.1f)
        {
            float groundHeight = GetGroundHeightAtPosition(x, z);
            return groundHeight <= maxAllowedHeight;
        }

        /// <summary>
        /// Find a valid spawn position that is clear of terrain
        /// Tries the requested position first, then searches nearby
        /// </summary>
        public Vector3Data FindClearSpawnPosition(float preferredX, float preferredZ, float searchRadius = 50f, int maxAttempts = 50)
        {
            // Try preferred position first
            if (IsPositionClear(preferredX, preferredZ))
            {
                return new Vector3Data { X = preferredX, Y = 0.5f, Z = preferredZ };
            }

            // Search outward in expanding rings for a clear position
            var random = new Random();
            for (int i = 0; i < maxAttempts; i++)
            {
                // Bias toward further distances to escape large hills
                float minDist = (i / (float)maxAttempts) * searchRadius * 0.5f;
                float angle = (float)(random.NextDouble() * Math.PI * 2);
                float distance = minDist + (float)(random.NextDouble() * (searchRadius - minDist));
                float testX = preferredX + (float)Math.Cos(angle) * distance;
                float testZ = preferredZ + (float)Math.Sin(angle) * distance;

                if (IsPositionClear(testX, testZ))
                {
                    return new Vector3Data { X = testX, Y = 0.5f, Z = testZ };
                }
            }

            // Systematic grid search as fallback - covers a wide area
            for (float gx = -60f; gx <= 60f; gx += 5f)
            {
                for (float gz = -60f; gz <= 60f; gz += 5f)
                {
                    if (IsPositionClear(gx, gz))
                    {
                        _logger.LogInformation($"Found clear spawn via grid search at ({gx}, {gz})");
                        return new Vector3Data { X = gx, Y = 0.5f, Z = gz };
                    }
                }
            }

            // Last resort: log warning and return origin (should never happen with reasonable terrain)
            _logger.LogWarning($"Could not find ANY clear spawn position! Terrain may cover entire play area.");
            return new Vector3Data { X = 0f, Y = 0.5f, Z = 0f };
        }

        /// <summary>
        /// Load a hill set (add to active terrain)
        /// </summary>
        public bool LoadHillSet(string hillSetName)
        {
            if (!_hillSets.ContainsKey(hillSetName))
            {
                _logger.LogWarning($"Hill set '{hillSetName}' not found");
                return false;
            }

            if (_activeHillSets.Contains(hillSetName))
            {
                _logger.LogInformation($"Hill set '{hillSetName}' already loaded");
                return true;
            }

            _activeHillSets.Add(hillSetName);
            RefreshActiveHills();

            _logger.LogInformation($"Loaded hill set '{hillSetName}'. Active hills: {_cachedActiveHills.Count}");
            return true;
        }

        /// <summary>
        /// Unload a hill set (remove from active terrain)
        /// </summary>
        public bool UnloadHillSet(string hillSetName)
        {
            if (!_activeHillSets.Contains(hillSetName))
            {
                _logger.LogInformation($"Hill set '{hillSetName}' not currently loaded");
                return false;
            }

            _activeHillSets.Remove(hillSetName);
            RefreshActiveHills();

            _logger.LogInformation($"Unloaded hill set '{hillSetName}'. Active hills: {_cachedActiveHills.Count}");
            return true;
        }

        /// <summary>
        /// Add a new hill set
        /// </summary>
        public void AddHillSet(string name, List<HillData> hills)
        {
            _hillSets[name] = hills;
            _logger.LogInformation($"Added hill set '{name}' with {hills.Count} hills");
        }

        /// <summary>
        /// Get list of all available hill sets
        /// </summary>
        public List<string> GetAvailableHillSets()
        {
            return _hillSets.Keys.ToList();
        }

        /// <summary>
        /// Get list of currently active hill sets
        /// </summary>
        public List<string> GetActiveHillSets()
        {
            return _activeHillSets.ToList();
        }

        /// <summary>
        /// Refresh the cached active hills list
        /// </summary>
        private void RefreshActiveHills()
        {
            _cachedActiveHills.Clear();

            foreach (var hillSetName in _activeHillSets)
            {
                if (_hillSets.TryGetValue(hillSetName, out var hills))
                {
                    _cachedActiveHills.AddRange(hills);
                }
            }
        }

        /// <summary>
        /// Calculate the height of a specific hill at the given X,Z position
        /// This mirrors the client-side sphere collision math
        /// </summary>
        private float CalculateHillHeightAtPosition(HillData hill, float x, float z)
        {
            // Calculate distance from hill center (X,Z only)
            float dx = x - hill.Position.X;
            float dz = z - hill.Position.Z;
            float horizontalDistance = (float)Math.Sqrt(dx * dx + dz * dz);

            // Check if point is within the hill's horizontal radius
            float maxHorizontalRadius = Math.Max(hill.Scale.X, hill.Scale.Z) / 2f;

            if (horizontalDistance > maxHorizontalRadius)
            {
                return 0f; // Outside the hill
            }

            // Calculate height using ellipsoid equation
            // The hill is a flattened sphere (ellipsoid) embedded 80% into the ground
            float normalizedX = dx / (hill.Scale.X / 2f);
            float normalizedZ = dz / (hill.Scale.Z / 2f);

            // Ellipsoid equation: x²/a² + y²/b² + z²/c² = 1
            // Solve for y: y = b * sqrt(1 - x²/a² - z²/c²)
            float normalizedDistanceSquared = normalizedX * normalizedX + normalizedZ * normalizedZ;

            if (normalizedDistanceSquared >= 1f)
            {
                return 0f; // Outside the ellipsoid
            }

            float normalizedHeight = (float)Math.Sqrt(1f - normalizedDistanceSquared);
            float fullEllipsoidHeight = normalizedHeight * (hill.Scale.Y / 2f);

            // The sphere top is at position.Y + fullEllipsoidHeight
            // position.Y is already set low (scale.Y * 0.1) to embed the sphere,
            // so no additional offset is needed
            return Math.Max(0f, hill.Position.Y + fullEllipsoidHeight);
        }

        /// <summary>
        /// Initialize default hill sets
        /// </summary>
        private void InitializeDefaultHillSets()
        {
            // Default hill set - the original hills
            var defaultHills = new List<HillData>
            {
                // Large hills (distant) - very flat and wide
                new HillData { Id = "hill_01", Name = "Northern Peak", HillSet = "default", Position = new Vector3Data { X = 30f, Y = 4f * 0.1f, Z = 40f }, Scale = new Vector3Data { X = 25f, Y = 4f, Z = 20f }, Color = new ColorData { R = 0.3f, G = 0.6f, B = 0.2f } },
                new HillData { Id = "hill_02", Name = "Western Heights", HillSet = "default", Position = new Vector3Data { X = -45f, Y = 5f * 0.1f, Z = 30f }, Scale = new Vector3Data { X = 30f, Y = 5f, Z = 25f }, Color = new ColorData { R = 0.35f, G = 0.65f, B = 0.25f } },
                new HillData { Id = "hill_03", Name = "Eastern Ridge", HillSet = "default", Position = new Vector3Data { X = 50f, Y = 3.5f * 0.1f, Z = -35f }, Scale = new Vector3Data { X = 22f, Y = 3.5f, Z = 18f }, Color = new ColorData { R = 0.4f, G = 0.7f, B = 0.3f } },

                // Medium hills - moderately flat
                new HillData { Id = "hill_04", Name = "Central Mound", HillSet = "default", Position = new Vector3Data { X = -25f, Y = 3f * 0.1f, Z = -20f }, Scale = new Vector3Data { X = 18f, Y = 3f, Z = 15f }, Color = new ColorData { R = 0.38f, G = 0.68f, B = 0.28f } },
                new HillData { Id = "hill_05", Name = "Sunrise Hill", HillSet = "default", Position = new Vector3Data { X = 15f, Y = 2.5f * 0.1f, Z = 25f }, Scale = new Vector3Data { X = 16f, Y = 2.5f, Z = 12f }, Color = new ColorData { R = 0.42f, G = 0.72f, B = 0.32f } },
                new HillData { Id = "hill_06", Name = "Far Meadow", HillSet = "default", Position = new Vector3Data { X = -10f, Y = 2.8f * 0.1f, Z = 45f }, Scale = new Vector3Data { X = 17f, Y = 2.8f, Z = 14f }, Color = new ColorData { R = 0.36f, G = 0.66f, B = 0.26f } },

                // Small hills (closer) - gentle mounds
                new HillData { Id = "hill_07", Name = "Little Knoll", HillSet = "default", Position = new Vector3Data { X = 12f, Y = 2f * 0.1f, Z = 8f }, Scale = new Vector3Data { X = 12f, Y = 2f, Z = 10f }, Color = new ColorData { R = 0.45f, G = 0.75f, B = 0.35f } },
                new HillData { Id = "hill_08", Name = "Nearby Rise", HillSet = "default", Position = new Vector3Data { X = -8f, Y = 1.5f * 0.1f, Z = 12f }, Scale = new Vector3Data { X = 10f, Y = 1.5f, Z = 8f }, Color = new ColorData { R = 0.43f, G = 0.73f, B = 0.33f } },
                new HillData { Id = "hill_09", Name = "South Bump", HillSet = "default", Position = new Vector3Data { X = 20f, Y = 1.8f * 0.1f, Z = -15f }, Scale = new Vector3Data { X = 14f, Y = 1.8f, Z = 11f }, Color = new ColorData { R = 0.41f, G = 0.71f, B = 0.31f } },
                new HillData { Id = "hill_10", Name = "Close Mound", HillSet = "default", Position = new Vector3Data { X = -18f, Y = 1.6f * 0.1f, Z = 5f }, Scale = new Vector3Data { X = 11f, Y = 1.6f, Z = 9f }, Color = new ColorData { R = 0.44f, G = 0.74f, B = 0.34f } },

                // Rolling hills (medium distance) - elongated and flat
                new HillData { Id = "hill_11", Name = "Rolling Ridge", HillSet = "default", Position = new Vector3Data { X = 35f, Y = 2.5f * 0.1f, Z = 15f }, Scale = new Vector3Data { X = 20f, Y = 2.5f, Z = 12f }, Color = new ColorData { R = 0.37f, G = 0.67f, B = 0.27f } },
                new HillData { Id = "hill_12", Name = "Distant Plateau", HillSet = "default", Position = new Vector3Data { X = -30f, Y = 3.2f * 0.1f, Z = -40f }, Scale = new Vector3Data { X = 24f, Y = 3.2f, Z = 15f }, Color = new ColorData { R = 0.39f, G = 0.69f, B = 0.29f } }
            };

            _hillSets["default"] = defaultHills;

            // Example additional hill sets for different scenarios
            var mountainSet = new List<HillData>
            {
                new HillData { Id = "mountain_01", Name = "Great Peak", HillSet = "mountains", Position = new Vector3Data { X = 0f, Y = 15f * 0.1f, Z = 0f }, Scale = new Vector3Data { X = 40f, Y = 15f, Z = 40f }, Color = new ColorData { R = 0.25f, G = 0.5f, B = 0.2f } },
                new HillData { Id = "mountain_02", Name = "Twin Peak North", HillSet = "mountains", Position = new Vector3Data { X = -60f, Y = 12f * 0.1f, Z = 60f }, Scale = new Vector3Data { X = 35f, Y = 12f, Z = 30f }, Color = new ColorData { R = 0.3f, G = 0.55f, B = 0.25f } },
                new HillData { Id = "mountain_03", Name = "Twin Peak South", HillSet = "mountains", Position = new Vector3Data { X = 60f, Y = 12f * 0.1f, Z = -60f }, Scale = new Vector3Data { X = 35f, Y = 12f, Z = 30f }, Color = new ColorData { R = 0.3f, G = 0.55f, B = 0.25f } }
            };

            _hillSets["mountains"] = mountainSet;

            var plainsSet = new List<HillData>
            {
                new HillData { Id = "plain_01", Name = "Gentle Rise", HillSet = "plains", Position = new Vector3Data { X = 25f, Y = 1f * 0.1f, Z = 25f }, Scale = new Vector3Data { X = 30f, Y = 1f, Z = 30f }, Color = new ColorData { R = 0.5f, G = 0.8f, B = 0.4f } },
                new HillData { Id = "plain_02", Name = "Meadow Swell", HillSet = "plains", Position = new Vector3Data { X = -25f, Y = 0.8f * 0.1f, Z = -25f }, Scale = new Vector3Data { X = 25f, Y = 0.8f, Z = 25f }, Color = new ColorData { R = 0.52f, G = 0.82f, B = 0.42f } }
            };

            _hillSets["plains"] = plainsSet;
        }

        /// <summary>
        /// Data structure for a hill
        /// </summary>
        public class HillData
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string HillSet { get; set; } = "";
            public Vector3Data Position { get; set; } = new();
            public Vector3Data Scale { get; set; } = new();
            public ColorData Color { get; set; } = new();
        }

        /// <summary>
        /// Color data structure
        /// </summary>
        public class ColorData
        {
            public float R { get; set; }
            public float G { get; set; }
            public float B { get; set; }
            public float A { get; set; } = 1f;
        }
    }

    /// <summary>
    /// Terrain data for client synchronization
    /// </summary>
    public class ServerTerrainData
    {
        public float BaseGroundLevel { get; set; }
        public List<TerrainHill> Hills { get; set; } = new();
        public List<string> ActiveHillSets { get; set; } = new();
    }

    /// <summary>
    /// Hill data for client communication
    /// </summary>
    public class TerrainHill
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string HillSet { get; set; } = "";
        public Vector3Data Position { get; set; } = new();
        public Vector3Data Scale { get; set; } = new();
        public TerrainService.ColorData Color { get; set; } = new();
    }
}