using System;
using Godot;

namespace ComputeShader.Slime;

public class AgentManager {

	private Random random = new Random();
	public AgentSettings settings;

	public AgentManager(int width, int height, float baseSpeed) {
		settings = BuildSettings(width, height, baseSpeed);
	}

	public AgentSettings BuildSettings(int width, int height, float baseSpeed) {
		return new AgentSettings {
			numAgents = 300000,
			width = width,
			height = height,
			delta = 0f,
			time = 0f,
			moveSpeed = 20f * baseSpeed,
			sensorSize = 1,
			sensorOffsetDst = 25.0f,
			sensorAngleDegrees = 30.0f,
			turnSpeed = 1f * (baseSpeed * 0.5f),
			trailWeight = 40f,
			maxLifetime = 40,
			mouseX = 0,
			mouseY = 0,
			mouseLeft = false,
			mouseRight = false,
			spawnType = (int) AgentSpawnType.Other,
		};
	}
	
	public Agent[] InitializeAgents() {
	    
		var spawnMode = (AgentSpawnType) settings.spawnType;
	    
		Vector2 center = new Vector2(settings.width / 2f, settings.height / 2f);
		Vector2 left = new Vector2(settings.width / 4f, settings.height / 2f);
		Vector2 right = new Vector2(3f * settings.width / 4f, settings.height / 2f);
		Vector2 startPos = Vector2.Zero;


		// Initialize Agents
		var agents = new Agent[settings.numAgents];
		for (int i = 0; i < settings.numAgents; i++) {
			uint type = random.NextDouble() > 0.6 ? 1u : 0;

			float randomAngle = RandomAngle();
			float angle = 0;

			if (spawnMode == AgentSpawnType.Other) {
				angle = randomAngle;
				if (type == 0) {
					startPos = center + InsideUnitCircle() * settings.height * 0.15f;
					angle = Mathf.Atan2((center - startPos).Normalized().Y, (center - startPos).Normalized().X);
				}
				else {
					if (random.NextDouble() > 0.5) {
						startPos = left + InsideUnitCircle() * settings.height * 0.15f;
					}
					else {
						startPos = right + InsideUnitCircle() * settings.height * 0.15f;
					}
				}
			}
			else {
				if (spawnMode == AgentSpawnType.Point) {
					startPos = center;
					angle = randomAngle;
				}
				else if (spawnMode == AgentSpawnType.Random) {
					startPos = new Vector2(random.Next(0, settings.width), random.Next(0, settings.height));
					angle = randomAngle;
				}
				else if (spawnMode == AgentSpawnType.InwardCircle) {
					startPos = center + InsideUnitCircle() * settings.height * 0.5f;
					angle = Mathf.Atan2((center - startPos).Normalized().Y, (center - startPos).Normalized().X);
				}
				else if (spawnMode == AgentSpawnType.RandomCircle) {
					startPos = center + InsideUnitCircle() * settings.height * 0.15f;
					angle = randomAngle;
				}
			}

			agents[i] = new Agent {
				X = startPos.X,
				Y = startPos.Y,
				Angle = angle,
				type = type,
				lifetime = settings.maxLifetime
			};
		}

		return agents;
	}

	private float RandomAngle() {
		// Generate random components on the unit circle
		double x = random.NextDouble() * 2 - 1;
		double y = random.NextDouble() * 2 - 1;

		// Calculate the angle in radians
		double angle = Math.Atan2(y, x);

		return (float) angle;
	}

	private Vector2 InsideUnitCircle() {
		double theta = 2.0 * Math.PI * random.NextDouble(); // angle
		double r = Math.Sqrt(random.NextDouble()); // radius

		float x = (float) (r * Math.Cos(theta));
		float y = (float) (r * Math.Sin(theta));

		return new Vector2(x, y);
	}
}


public enum AgentSpawnType {
	Random,
	Point,
	InwardCircle,
	RandomCircle,
	Other
}

public struct Agent {
	public float X;
	public float Y;
	public float Angle;
	public uint type;
	public float lifetime;
}

public struct AgentSettings {
	public uint numAgents;
	public int width;
	public int height;
	public float delta;
	public float time;
	public float moveSpeed;
	public int sensorSize;
	public float sensorOffsetDst;
	public float sensorAngleDegrees;
	public float turnSpeed;
	public float trailWeight;
	public float maxLifetime;
	public float mouseX;
	public float mouseY;
	public bool mouseLeft;
	public bool mouseRight;
	public int spawnType;
};