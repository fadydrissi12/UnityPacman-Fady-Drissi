using System.Collections.Generic;
using UnityEngine;

public sealed class PacmanGame : MonoBehaviour
{
    private const int Width = 21;
    private const int Height = 21;
    private const float CellSize = 1.25f;

    private static readonly string[] Maze =
    {
        "#####################",
        "#.........#.........#",
        "#.###.###.#.###.###.#",
        "#...................#",
        "#.###.#.#####.#.###.#",
        "#.....#...#...#.....#",
        "#####.###.#.###.#####",
        "    #.#.......#.#    ",
        "#####.#.## ##.#.#####",
        "      .... ....      ",
        "#####.#.## ##.#.#####",
        "    #.#.......#.#    ",
        "#####.#.#####.#.#####",
        "#.........#.........#",
        "#.###.###.#.###.###.#",
        "#...#...........#...#",
        "###.#.#.#####.#.#.###",
        "#.....#...#...#.....#",
        "#.########.########.#",
        "#...................#",
        "#####################"
    };

    private readonly Dictionary<Vector2Int, GameObject> pellets = new();
    private readonly List<Transform> enemies = new();
    private readonly List<GameObject> spawnedObjects = new();
    private Transform pacman;
    private Vector2Int playerCell = new(3, 15);
    private Vector2Int currentDirection = Vector2Int.right;
    private Vector2Int requestedDirection = Vector2Int.right;
    private float nextMoveTime;
    private int score;
    private bool gameOver;
    private Material wallMaterial;
    private Material yellowMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<PacmanGame>() == null)
            new GameObject("Pacman Game").AddComponent<PacmanGame>();
    }

    private void Awake()
    {
        wallMaterial = CreateMaterial(new Color(0.02f, 0.2f, 1f));
        yellowMaterial = CreateMaterial(new Color(1f, 0.82f, 0.02f));
        CreateCamera();
        BuildMaze();
        CreatePacman();
        CreateEnemies();
    }

    private void Update()
    {
        ReadControls();
        if (gameOver) return;
        if (Time.time >= nextMoveTime)
        {
            nextMoveTime = Time.time + 0.07f;
            MovePacman();
            CheckEnemyCollision();
            if (gameOver) return;
            MoveEnemies();
            CollectPellet();
        }
    }

    private void ReadControls()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) requestedDirection = Vector2Int.down;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) requestedDirection = Vector2Int.up;
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) requestedDirection = Vector2Int.left;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) requestedDirection = Vector2Int.right;
    }

    private void BuildMaze()
    {
        CreateCube("Floor", GridToWorld(new Vector2Int(10, 10)) + Vector3.down * 0.35f,
            new Vector3(27f, 0.5f, 27f), CreateMaterial(new Color(0.01f, 0.015f, 0.04f)));

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (Maze[y][x] == '#' && !IsDoor(cell))
                    CreateCube("Blue Wall", GridToWorld(cell) + Vector3.up * 0.5f,
                        new Vector3(1.12f, 1f, 1.12f), wallMaterial);
                else if (Maze[y][x] == '.')
                    CreatePellet(cell);
            }
        }

        CreateDoor("Door Left", new Vector2Int(0, 9));
        CreateDoor("Door Right", new Vector2Int(Width - 1, 9));
        CreateDoor("Door Top", new Vector2Int(10, 0));
        CreateDoor("Door Bottom", new Vector2Int(10, Height - 1));
    }

    private void CreatePellet(Vector2Int cell)
    {
        var pellet = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pellet.name = "Pellet";
        pellet.transform.position = GridToWorld(cell) + Vector3.up * 0.25f;
        pellet.transform.localScale = new Vector3(0.16f, 0.22f, 0.16f);
        pellet.GetComponent<Renderer>().material = yellowMaterial;
        pellets[cell] = pellet;
        spawnedObjects.Add(pellet);
    }

    private void CreateDoor(string name, Vector2Int cell)
    {
        CreateCube(name, GridToWorld(cell) + Vector3.up * 0.7f,
            new Vector3(1.1f, 0.15f, 0.3f), yellowMaterial);
    }

    private void CreatePacman()
    {
        pacman = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        pacman.name = "Pacman (Yellow Sphere)";
        pacman.localScale = Vector3.one * 0.9f;
        pacman.GetComponent<Renderer>().material = yellowMaterial;
        spawnedObjects.Add(pacman.gameObject);
        Place(pacman, playerCell);
    }

    private void CreateEnemies()
    {
        var positions = new[] { new Vector2Int(9, 9), new Vector2Int(11, 9), new Vector2Int(9, 10), new Vector2Int(11, 10) };
        var colors = new[] { Color.red, Color.magenta, Color.cyan, new Color(1f, 0.45f, 0.05f) };
        for (var i = 0; i < positions.Length; i++)
        {
            var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
            enemy.name = "Enemy " + (i + 1);
            enemy.localScale = Vector3.one * 0.72f;
            enemy.GetComponent<Renderer>().material = CreateMaterial(colors[i]);
            spawnedObjects.Add(enemy.gameObject);
            Place(enemy, positions[i]);
            enemies.Add(enemy);
        }
    }

    private void MovePacman()
    {
        if (CanEnter(playerCell + requestedDirection))
            currentDirection = requestedDirection;
        var nextCell = playerCell + currentDirection;
        if (nextCell.x < 0) nextCell.x = Width - 1;
        if (nextCell.x >= Width) nextCell.x = 0;
        if (nextCell.y < 0) nextCell.y = Height - 1;
        if (nextCell.y >= Height) nextCell.y = 0;
        if (IsDoor(playerCell) && !CanEnter(playerCell + currentDirection))
        {
            playerCell = nextCell;
            Place(pacman, playerCell);
            return;
        }
        if (!CanEnter(nextCell)) return;
        playerCell = nextCell;
        Place(pacman, playerCell);
    }

    private void TeleportThroughDoor()
    {
        if (playerCell.x == 0) playerCell = new Vector2Int(Width - 1, playerCell.y);
        else if (playerCell.x == Width - 1) playerCell = new Vector2Int(0, playerCell.y);
        else if (playerCell.y == 0) playerCell = new Vector2Int(playerCell.x, Height - 1);
        else if (playerCell.y == Height - 1) playerCell = new Vector2Int(playerCell.x, 0);
        Place(pacman, playerCell);
    }

    private void MoveEnemies()
    {
        foreach (var enemy in enemies)
        {
            var cell = WorldToGrid(enemy.position);
            var options = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            var option = options[Random.Range(0, options.Length)];
            if (CanEnter(cell + option))
                Place(enemy, cell + option);
            if (WorldToGrid(enemy.position) == playerCell)
            {
                LoseGame();
                return;
            }
        }
    }

    private void CheckEnemyCollision()
    {
        foreach (var enemy in enemies)
        {
            if (WorldToGrid(enemy.position) == playerCell)
            {
                LoseGame();
                return;
            }
        }
    }

    private void CollectPellet()
    {
        if (!pellets.TryGetValue(playerCell, out var pellet)) return;
        Destroy(pellet);
        pellets.Remove(playerCell);
        score++;
        Debug.Log("Score : " + score);
    }

    private void LoseGame()
    {
        gameOver = true;
        Debug.Log("5sert");
    }

    private bool CanEnter(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height
            && (Maze[cell.y][cell.x] != '#' || IsDoor(cell));
    }

    private static bool IsDoor(Vector2Int cell)
    {
        return cell == new Vector2Int(0, 9)
            || cell == new Vector2Int(Width - 1, 9)
            || cell == new Vector2Int(10, 0)
            || cell == new Vector2Int(10, Height - 1);
    }

    private void CreateCamera()
    {
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 28f, 0f);
        cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 14.5f;
        camera.backgroundColor = new Color(0.01f, 0.015f, 0.04f);
        spawnedObjects.Add(cameraObject);
    }

    private void Place(Transform item, Vector2Int cell)
    {
        item.position = GridToWorld(cell) + Vector3.up * 0.65f;
    }

    private static Vector3 GridToWorld(Vector2Int cell)
    {
        return new Vector3((cell.x - 10) * CellSize, 0f, (10 - cell.y) * CellSize);
    }

    private static Vector2Int WorldToGrid(Vector3 position)
    {
        return new Vector2Int(Mathf.RoundToInt(position.x / CellSize) + 10,
            10 - Mathf.RoundToInt(position.z / CellSize));
    }

    private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.position = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().material = material;
        if (FindAnyObjectByType<PacmanGame>() is PacmanGame game)
            game.spawnedObjects.Add(cube);
        return cube;
    }

    private static Material CreateMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogError("Aucun shader graphique n'est disponible dans le projet Unity.");
            return null;
        }
        var material = new Material(shader);
        material.color = color;
        return material;
    }

    private void OnGUI()
    {
        GUI.color = Color.white;
        GUI.Label(new Rect(20f, 20f, 300f, 30f), "PACMAN    Score : " + score);
        GUI.Label(new Rect(20f, 45f, 500f, 30f), "Fleches ou WASD pour deplacer Pacman");
        if (!gameOver) return;

        var messageStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 42,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        messageStyle.normal.textColor = Color.red;
        GUI.Label(new Rect(Screen.width / 2f - 150f, Screen.height / 2f - 90f, 300f, 65f), "5sert", messageStyle);

        var buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold
        };
        if (GUI.Button(new Rect(Screen.width / 2f - 80f, Screen.height / 2f, 160f, 55f), "3awed", buttonStyle))
            RestartGame();
    }

    private void RestartGame()
    {
        foreach (var spawnedObject in spawnedObjects)
        {
            if (spawnedObject != null)
                Destroy(spawnedObject);
        }

        spawnedObjects.Clear();
        pellets.Clear();
        enemies.Clear();
        pacman = null;
        playerCell = new Vector2Int(3, 15);
        currentDirection = Vector2Int.right;
        requestedDirection = Vector2Int.right;
        nextMoveTime = Time.time;
        score = 0;
        gameOver = false;
        CreateCamera();
        BuildMaze();
        CreatePacman();
        CreateEnemies();
    }
}
