using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ComputeShader.Life;

using System;
using Godot;

public class OrganismManager {
    private Random random = new Random();
    public OrganismSettings settings;
    private readonly uint numOrganisms;
    private readonly int numSpecies;

    public OrganismManager(uint numOrganisms, int numSpecies, int width, int height) {
        this.numOrganisms = numOrganisms;
        this.numSpecies = numSpecies;
        settings = BuildSettings(width, height);
    }

    public OrganismSettings BuildSettings(int width, int height) {
        return new OrganismSettings {
            numOrganisms = numOrganisms,
            width = width,
            height = height,
            delta = 0.01f,
            sensorSize = 10,
            spawnType = (int)OrganismSpawnType.RandomCircle,
            numSpecies = numSpecies,
        };
    }

    public Organism[] Initialize() {
        var spawnMode = (OrganismSpawnType)settings.spawnType;

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
                    type = (uint)(i % 4),
                }
            };
        }

        return organisms;
    }

    private Vector2 InsideUnitCircle() {
        double theta = 2.0 * Math.PI * random.NextDouble(); // angle
        double r = Math.Sqrt(random.NextDouble()); // radius

        float x = (float)(r * Math.Cos(theta));
        float y = (float)(r * Math.Sin(theta));

        return new Vector2(x, y);
    }
    
    /**
	 * Create bins here and pass them to the GPU to constrain our search space.
	 *
	 * We might want to do a list of lists, then have a struct for each that says
	 * for each bin, where it starts and stops.
	 *
	 * Then for each organism, which bin it belongs to.
	 */
	public BinArrayBuffers BuildOrganismBins(OrganismPosition[] organismPositions) {
	    var numBinsX = settings.width / settings.sensorSize;
	    var numBinsY = settings.height / settings.sensorSize;
	    var sensorRecip = 1.0f / settings.sensorSize;
	    
	    var binnedIndices = new List<int>[numBinsX, numBinsY];
		var organismBinMembership = new int[organismPositions.Length];
		var flatBinnedOrganismIndices = new List<int>();
		var bins = new List<Bin>();

		for (int i = 0; i < organismPositions.Length; i++) {
			var organism = organismPositions[i];
			var binX = (int) (organism.X * sensorRecip);
			var binY = (int) (organism.Y * sensorRecip);

			binnedIndices[binX, binY] ??= new List<int>();
			binnedIndices[binX, binY].Add(i);
			organismBinMembership[i] = binX + binY * numBinsX;
		}
		
		for (int j = 0; j < numBinsY; j++) {
			var row = j * numBinsX;
			for (int i = 0; i < numBinsX; i++) {
				binnedIndices[i, j] ??= new List<int>();
				var binnedOrganismIndices = binnedIndices[i, j];
				var start = flatBinnedOrganismIndices.Count;
				var end = start + binnedOrganismIndices.Count;
				flatBinnedOrganismIndices.AddRange(binnedOrganismIndices);
				var bin = new Bin {
					start = start,
					end = end,
					left = (i - 1 + numBinsX) % numBinsX + j * numBinsX,
					upLeft = (i - 1 + numBinsX) % numBinsX + ((j - 1 + numBinsY) % numBinsY) * numBinsX,
					up = i + ((j - 1 + numBinsY) % numBinsY) * numBinsX,
					upRight = (i + 1) % numBinsX + (j - 1 + numBinsY) % numBinsY * numBinsX,
					right = (i + 1) % numBinsX + j * numBinsX,
					downRight = (i + 1) % numBinsX + (j + 1) % numBinsY * numBinsX,
					down = i + (j + 1) % numBinsY * numBinsX,
					downLeft = (i - 1 + numBinsX) % numBinsX + (j + 1) % numBinsY * numBinsX,
				};
				bins.Add(bin);
			}
		}

		return new BinArrayBuffers {
			organismBinMembership = organismBinMembership,
			binsArray = bins.ToArray(),
			flatBinnedIndices = flatBinnedOrganismIndices.ToArray()
		};
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
    public int sensorSize;
    public int spawnType;
    public int numSpecies;
    public int frameNum;
};