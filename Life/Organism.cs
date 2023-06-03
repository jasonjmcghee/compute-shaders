using System.Runtime.InteropServices;

namespace ComputeShader.Life;

using System;
using Godot;

public class OrganismManager {
    private Random random = new Random();
    public OrganismSettings settings;
    private readonly uint numOrganisms;
    private readonly int numSpecies;

    public OrganismManager(uint numOrganisms, int numSpecies, int width, int height, float baseSpeed) {
        this.numOrganisms = numOrganisms;
        this.numSpecies = numSpecies;
        settings = BuildSettings(width, height, baseSpeed);
    }

    public OrganismSettings BuildSettings(int width, int height, float baseSpeed) {
        return new OrganismSettings {
            numOrganisms = numOrganisms,
            width = width,
            height = height,
            delta = 0f,
            moveSpeed = 1f * baseSpeed,
            sensorSize = 10,
            spawnType = (int) OrganismSpawnType.RandomCircle,
            numSpecies = numSpecies,
        };
    }

    public Organism[] Initialize() {
        var spawnMode = (OrganismSpawnType) settings.spawnType;

        Vector2 center = new Vector2(settings.width / 2f, settings.height / 2f);
        Vector2 left = new Vector2(settings.width / 4f, settings.height / 2f);
        Vector2 right = new Vector2(3f * settings.width / 4f, settings.height / 2f);
        Vector2 startPos = Vector2.Zero;

        // Initialize Organisms
        var organisms = new Organism[settings.numOrganisms];
        for (int i = 0; i < settings.numOrganisms; i++) {
            if (spawnMode == OrganismSpawnType.Other) {
                startPos = center + InsideUnitCircle() * settings.height * 0.15f;
            }
            else {
                if (spawnMode == OrganismSpawnType.Point) {
                    startPos = center;
                }
                else if (spawnMode == OrganismSpawnType.Random) {
                    startPos = new Vector2(random.Next(0, settings.width), random.Next(0, settings.height));
                }
                else if (spawnMode == OrganismSpawnType.InwardCircle) {
                    startPos = center + InsideUnitCircle() * settings.height * 0.5f;
                }
                else if (spawnMode == OrganismSpawnType.RandomCircle) {
                    startPos = center + InsideUnitCircle() * settings.height * 0.4f;
                }
            }

            organisms[i] = new Organism {
                OrganismPosition = new OrganismPosition {
                    X = startPos.X,
                    Y = startPos.Y,
                },
                OrganismInfo = new OrganismInfo {
                    velX = 0,
                    velY = 0,
                    type = (uint) (i % 4) + 1,
                }
            };
        }

        return organisms;
    }

    private Vector2 InsideUnitCircle() {
        double theta = 2.0 * Math.PI * random.NextDouble(); // angle
        double r = Math.Sqrt(random.NextDouble()); // radius

        float x = (float) (r * Math.Cos(theta));
        float y = (float) (r * Math.Sin(theta));

        return new Vector2(x, y);
    }
}

public enum OrganismSpawnType {
    Random,
    Point,
    InwardCircle,
    RandomCircle,
    Other
}

public struct Organism {
    public OrganismPosition OrganismPosition;
    public OrganismInfo OrganismInfo;
}

public struct OrganismPosition {
    public float X;
    public float Y;
}

public struct OrganismInfo {
    public float velX;
    public float velY;
    public uint type;
}

public struct OrganismSettings {
    public uint numOrganisms;
    public int width;
    public int height;
    public float delta;
    public float moveSpeed;
    public int sensorSize;
    public int spawnType;
    public int numSpecies;
    public int frameNum;
};