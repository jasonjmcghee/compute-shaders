public struct DiffusionSettings {
	public int width;
	public int height;
	public float delta;
	public float evaporateSpeed;
	public float diffuseSpeed;
	public float mouseX;
	public float mouseY;
	public bool mouseLeft;
	public bool mouseRight;
};

public class DiffusionManager {
	public readonly DiffusionSettings settings;

	public DiffusionManager(int width, int height, float baseSpeed) {
		settings = BuildSettings(width, height, baseSpeed);
	}

	public DiffusionSettings BuildSettings(int width, int height, float baseSpeed) {
		return new DiffusionSettings {
			width = width,
			height = height,
			delta = 0f,
			evaporateSpeed = 0.1f * baseSpeed * 2,
			diffuseSpeed = 3f * baseSpeed * 2,
			mouseX = 0,
			mouseY = 0,
			mouseLeft = false,
			mouseRight = false,
		};
	}
}
