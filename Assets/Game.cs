using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.LowLevel;
using UnityEngine.SceneManagement;

[CreateAssetMenu]
public class Game : ScriptableObject
{
    float timer = 0;

    [SerializeField]
    private GameObject ball;
    [SerializeField]
    private GameObject player;
    [SerializeField]
    private GameObject destroyable;
    [SerializeField]
    private GameObject vfx;
    [SerializeField]
    private GameObject _winUi;
    private Button _playAgainButton;
    [SerializeField]
    private GameObject _loseUi;
    private Button _replayButton;
    [SerializeField]
    private string _playSceneName;
    [SerializeField]
    private float _ballScale;
    [SerializeField]
    private LayerMask _destroyableLayer;
    [SerializeField]
    private LayerMask _vertical;
    [SerializeField]
    private LayerMask _horizontal;
    [SerializeField]
    private bool _logging;
    [SerializeField]
    private float playerSpeedScale = 2;
    [SerializeField]
    private float ballSpeedScale = 4;
    [SerializeField]
    private float maxXPos;
    [SerializeField]
    private float minXPos;
    [SerializeField]
    private List<Vector3> _destroyablePositions;

    [field: NonSerialized]
    public bool IsInitialized { get; private set; }

    private GameObject _ball;
    private CircleCollider2D ballCollider;

    private GameObject _player;
    private List<GameObject> _playableItems = new();
    [SerializeField]
    private List<GameObject> _lifesGameObjects = new();
    private AudioSource _audioManager;
    private AudioSource _playerColide;
    private AudioSource _destroyableColide;
    private AudioSource _loseSound;
    private AudioSource _winSound;

    [SerializeField]
    private bool gameEnded;
    private Vector2 direction = new Vector2(1, 1).normalized;
    private float ballColliderRadius;
    [field: NonSerialized]
    private bool ballIsLaunched;
    private Vector3 initBallPos = new(0, -2.7f, 0);
    private Vector3 initPlayerPos = new(0, -3, 0);
    [SerializeField]
    private int lifes;
    public void OnEnable()
    {
        var playerLoop = PlayerLoop.GetCurrentPlayerLoop();

        var ourSubsystem = new PlayerLoopSystem();
        ourSubsystem.updateDelegate = CustomLoop;

        var newList = playerLoop.subSystemList.ToList();
        newList.Add(ourSubsystem);

        playerLoop.subSystemList = newList.ToArray();

        PlayerLoop.SetPlayerLoop(playerLoop);
    }

    private void CustomLoop()
    {
        if (Application.isPlaying && SceneManager.GetActiveScene().name == _playSceneName)
        {
            if (!IsInitialized)
            {
                Initialize();
            }

            MainUpdate();
        }
        else
        {
            if (IsInitialized)
            {
                Dispose();
            }
        }
    }

    private void Initialize()
    {
        gameEnded = false;
        _lifesGameObjects.Clear();
        lifes = 3;
        _winUi = GameObject.FindWithTag("WinUi");
        _playAgainButton = _winUi.GetComponentInChildren<Button>();
        _playAgainButton.onClick.AddListener(ReloadScene);
        _winUi.SetActive(false);
        _loseUi = GameObject.FindWithTag("LoseUi");
        _replayButton = _loseUi.GetComponentInChildren<Button>();
        _replayButton.onClick.AddListener(ReloadScene);
        _loseUi.SetActive(false);
        _lifesGameObjects.Add(GameObject.FindWithTag("life1"));
        _lifesGameObjects.Add(GameObject.FindWithTag("life2"));
        _lifesGameObjects.Add(GameObject.FindWithTag("life3"));
        _audioManager = GameObject.FindWithTag("soundManager").GetComponent<AudioSource>();
        _playerColide = GameObject.FindWithTag("playerSound").GetComponent<AudioSource>();
        _destroyableColide = GameObject.FindWithTag("destroyableSound").GetComponent<AudioSource>();
        _loseSound = GameObject.FindWithTag("loseSound").GetComponent<AudioSource>();
        _winSound = GameObject.FindWithTag("winSound").GetComponent<AudioSource>();

        _ball = Instantiate(ball, initBallPos, Quaternion.identity);
        _ball.transform.localScale = new Vector3(_ballScale, _ballScale, _ballScale);

        ballCollider = _ball.GetComponent<CircleCollider2D>();
        ballColliderRadius = ballCollider.radius * _ballScale;
        _playableItems.Add(_ball);

        _player = Instantiate(player);
        _player.transform.position = initPlayerPos;
        _playableItems.Add(_player);

        foreach (var position in _destroyablePositions)
        {
            var item = Instantiate(destroyable, position, Quaternion.identity);
            _playableItems.Add(item);
        }
        
        IsInitialized = true;
    }

    private void Dispose()
    {
        foreach (var playableItem in _playableItems)
        {
            //Destroying immediately due to being out of play mode
            DestroyImmediate(playableItem);
        }
        _playableItems.Clear();
        IsInitialized = false;
    }

    private void MainUpdate()
    {
        if (Input.GetKey(KeyCode.Space) && !gameEnded)
        {
            ballIsLaunched = true;
        }
        if (ballIsLaunched)
        {
            HandleBallMovement(out var move);
            HandlingBorderCollisions(move);
            HandlingPlayerInputs();

            HandleDestroyableCollisionWithBox(move);
        }
        
    }

    private void HandleBallMovement(out Vector2 move)
    {
        timer += Time.deltaTime;
        move = direction * Time.deltaTime * ballSpeedScale;

        _ball.transform.Translate(move);

        if (_logging)
        {
            Debug.Log($"Direction: {direction}");
        }
    }

    private void HandlingBorderCollisions(Vector2 move)
    {
        var size = ballColliderRadius * 2f;
        var verticalCollision = Physics2D.BoxCast(_ball.transform.position,
            new Vector2(size, size), 0f, direction, move.magnitude, _vertical);

        if (verticalCollision)
        {
            var target = verticalCollision.collider.gameObject;
            if (target.CompareTag("Player"))
            {
                _playerColide.Play();
                CalculateNewBallDirection(_player.transform.position, _ball.transform.position);
            }
            else if (target.CompareTag("Respawn"))
            {
                _audioManager.Play();
                _loseSound.Play();
                ballIsLaunched = false;
                _ball.transform.position = new Vector3(0,-2.7f,0);
                _player.transform.position = initPlayerPos;
                UpdateLifes();
            }
            else
            {
                float prewDirection = direction.y;
                direction = new Vector2(direction.x, prewDirection * -1);
                _ball.transform.position += new Vector3(0, -prewDirection * Time.deltaTime * 5, 0);
            }
        }

        
        var horizontalCollision = Physics2D.BoxCast(_ball.transform.position,
            new Vector2(size, size), 0f, direction, Vector2.down.magnitude, _horizontal);
        if (horizontalCollision)
        {
            float prewDirection = direction.x;
            direction = new Vector2(prewDirection * -1, direction.y);
            _ball.transform.position += new Vector3(-prewDirection * Time.deltaTime * 5, 0, 0);
        }
    }

    private void HandlingPlayerInputs()
    {
        var playerTransform = _player.transform;

        if (Input.GetKey(KeyCode.LeftArrow)&& _player.transform.position.x >= minXPos)
        {
            float prewPos = _player.transform.position.x;
            playerTransform.position = new Vector2(prewPos - playerSpeedScale * Time.deltaTime, playerTransform.position.y);
        }
        if (Input.GetKey(KeyCode.RightArrow) && _player.transform.position.x <= maxXPos)
        {
            float prewPos = _player.transform.position.x;
            playerTransform.position = new Vector2(prewPos + playerSpeedScale * Time.deltaTime, playerTransform.position.y);
        }
    }

    private void HandleDestroyableCollisionWithBox(Vector2 move)
    {
        var size = ballColliderRadius * 2f;
        var collision = Physics2D.BoxCast(_ball.transform.position,
            new Vector2(size, size), 0f, direction, move.magnitude, _destroyableLayer.value);

        if (collision.collider != null)
        {
            var fx = Instantiate(vfx, collision.transform.position, Quaternion.identity);
            Destroy(fx, 1.2f);
            _destroyableColide.Play();
            if (_logging)
            {
                Debug.Log(collision.normal);
            }

            var target = collision.collider.gameObject;
            var normal = collision.normal;

            if (normal.x == 0)
            {
                float previousDirection = direction.y;
                direction = new Vector2(direction.x, previousDirection * -1);
                _ball.transform.position += new Vector3(0, -previousDirection * Time.deltaTime * 10, 0);
            }
            else
            {
                float previousDirection = direction.x;
                direction = new Vector2(previousDirection * -1, direction.y);
                _ball.transform.position += new Vector3(-previousDirection * Time.deltaTime * 10, 0, 0);
            }
            
            Destroy(target);
            GameObject[] destroyable = GameObject.FindGameObjectsWithTag("destroyable");
           
            if (destroyable.Length == 1)
            {
                gameEnded = true;
                _audioManager.Stop();
                _winSound.Play();
                ballIsLaunched = false;
                _winUi.SetActive(true);
            }
        }
    }
    private void CalculateNewBallDirection(Vector3 playerPos, Vector3 ballPos)
    {
        //Здесь можно было бы использовать паттерн visitor, но тк варианта всего 4, я решил не перегружать код
        var distance = playerPos.x - ballPos.x;

        if (distance > 0.4f)
        {
            direction = new Vector2(-2, 1).normalized;
        }
        if (distance < 0.4 && distance >= 0)
        {
            direction = new Vector2(1, 1).normalized;
        }
        if (distance <= -0.4)
        {
            direction = new Vector2(2, 1).normalized;
        }
        if (distance >= -0.4 && distance < 0)
        {
            direction = new Vector2(-1, 1).normalized;
        }
    }

    private void UpdateLifes()
    {
        lifes--;

        for (int i = 0; i < _lifesGameObjects.Count; i++)
        {
            _lifesGameObjects[i].SetActive(i < lifes);
        }

        if (lifes <= 0)
        {
            gameEnded = true;
            _loseUi.SetActive(true);
        }
    }
    
    private void ReloadScene()
    {
        _playAgainButton.onClick.RemoveAllListeners();
        _replayButton.onClick.RemoveAllListeners();
        for (int i = 0; i < _lifesGameObjects.Count; i++)
        {
            _lifesGameObjects[i].SetActive(true);
        }
        IsInitialized = false;
        SceneManager.LoadScene(_playSceneName);
    }

}