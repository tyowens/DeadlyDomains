using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DamageIndicator : MonoBehaviour
{
    private Vector2 _worldPosition;
    private float _secondsLeft;

    // Start is called before the first frame update
    void Start()
    {
        _secondsLeft = 1f;
    }

    // Update is called once per frame
    void Update()
    {
        _secondsLeft -= Time.deltaTime;
        if (_secondsLeft <= 0f)
        {
            Destroy(gameObject);
        }

        _worldPosition += new Vector2(0f, 0.5f * Time.deltaTime);
        transform.position = Camera.main.WorldToScreenPoint(_worldPosition);
        GetComponent<TextMeshProUGUI>().alpha = Math.Min(_secondsLeft + 0.5f, 1f);
    }

    public void SetWorldPosition(Vector2 worldPosition)
    {
        _worldPosition = worldPosition;
    }
}
