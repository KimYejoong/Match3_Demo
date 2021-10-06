using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

public class NodePiece : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public int value;
    public Point index;

    [HideInInspector]
    public Vector2 pos;

    [HideInInspector]
    public RectTransform rect;

    private bool _updating;
    
    [HideInInspector]
    public bool isFalling;
    
    private Image _img;

    [SerializeField]
    private float gravity = 0.2f;

    private float _fallingSpeed;
    
    [SerializeField]
    private float initialSpeed = 2f; // initial speed when free falling
    [SerializeField]
    private float terminalSpeed = 8f; // terminal speed when free falling

    public void Initialize(int v, Point p, Sprite piece)
    {        
        _img = GetComponent<Image>();
        rect = GetComponent<RectTransform>();
        _fallingSpeed = 0f;

        value = v;
        SetIndex(p);
        _img.sprite = piece;
    }


    public void SetIndex(Point p)
    {
        index = p;
        ResetPosition();
        UpdateName();
    }

    public void ResetPosition()
    {
        pos = new Vector2(32 + (64 * index.x), -32 - (64 * index.y));
    }

    public void MovePositionTo(Vector2 move)
    {
        if (!isFalling)
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, move, Time.deltaTime * 16f);
        else
        {
            _fallingSpeed = Mathf.Clamp(initialSpeed, _fallingSpeed + gravity, terminalSpeed);
            rect.anchoredPosition += _fallingSpeed * Vector2.down;
        }
    }

    public bool UpdatePiece()
    {
        if (isFalling)
        {
            if (rect.anchoredPosition.y < pos.y)
            {
                rect.anchoredPosition = pos;
                _updating = false;
                isFalling = false;
                _fallingSpeed = 0f;
                return false;
            }
            else
            {
                MovePositionTo((pos));
                _updating = true;
                return true;
            }
        }
        else
        {
            if (Vector2.Distance(rect.anchoredPosition, pos) > 1)
            {
                MovePositionTo(pos);
                _updating = true;
                return true;
            }
            else
            {
                rect.anchoredPosition = pos;
                _updating = false;
                return false;
            }
        }
    }

    private void UpdateName()
    {
        transform.name = "Node [" + index.x + ", " + index.y + "]";
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (Match3.Instance.gameState != Match3.GameState.Started)
            return;

        if (!Match3.Instance.IsMovable())
            return;

        if (_updating)
            return;

        MovePieces.instance.MovePiece(this);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (Match3.Instance.gameState != Match3.GameState.Started)
            return;

        MovePieces.instance.DropPiece();
    }
}
