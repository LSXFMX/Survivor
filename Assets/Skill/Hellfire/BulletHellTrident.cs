using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 地狱三叉戟：在目标点播放“从上落下”的动画，
/// 到命中时刻结算伤害并销毁（不做真实位移下落）。
/// </summary>
public class BulletHellTrident : MonoBehaviour
{
    [Header("命中时序")]
    public bool useFrameTiming = true;
    public int totalFrames = 13;
    public int hitFrame = 6; // 1-based
    public float animationDuration = 1f;
    public float impactDelay = 0.2f;
    public float spawnOffsetY = 0.25f;
    public float destroyDelayAfterHit = 0.08f;

    [Header("朝向（适配45度视角）")]
    public Vector3 worldEuler = new Vector3(45f, 0f, 0f);

    enemy _targetEnemy;
    int _damage;
    Attribute _playerAttr;
    Skillbase _sourceSkill; // 用于 GameSessionTracker 记录伤害归属
    bool _started;
    bool _damageApplied;

    public void Setup(Skillbase sourceSkill, enemy targetEnemy, int damage)
    {
        _targetEnemy = targetEnemy;
        _damage = Mathf.Max(1, damage);
        _playerAttr = null;
        _sourceSkill = sourceSkill;
        if (sourceSkill != null && sourceSkill.player != null)
            _playerAttr = sourceSkill.player.GetComponent<Attribute>();

        // 由动画表现“从上往下”，脚本只把特效锚在目标处。
        if (_targetEnemy != null)
        {
            Vector3 p = _targetEnemy.transform.position;
            p.y += spawnOffsetY;
            transform.position = p;
        }
        transform.rotation = Quaternion.Euler(worldEuler);

        if (_started) return;
        _started = true;
        StartCoroutine(ImpactRoutine());
    }

    IEnumerator ImpactRoutine()
    {
        float delay = useFrameTiming ? GetFrameImpactDelay() : Mathf.Max(0f, impactDelay);
        yield return new WaitForSeconds(delay);

        TryApplyDamageOnce();

        // 命中后继续播放后半段“收回”动画，再销毁
        float tail = Mathf.Max(0f, animationDuration - delay);
        if (tail > 0f)
            yield return new WaitForSeconds(tail);
        else if (destroyDelayAfterHit > 0f)
            yield return new WaitForSeconds(destroyDelayAfterHit);

        Destroy(gameObject);
    }

    float GetFrameImpactDelay()
    {
        int frames = Mathf.Max(2, totalFrames);
        int frameIndex = Mathf.Clamp(hitFrame, 1, frames);
        float normalized = (frameIndex - 1f) / (frames - 1f);
        return Mathf.Max(0f, animationDuration) * normalized;
    }

    void TryApplyDamageOnce()
    {
        if (_damageApplied) return;
        _damageApplied = true;

        if (_targetEnemy == null) return;
        if (_targetEnemy.health <= 0 || _targetEnemy.rolestate == global::enemy.state.dead) return;

        float evaRoll = UnityEngine.Random.value * 100f;
        if (_targetEnemy.EVA > evaRoll)
        {
            // 敌人闪避成功：在敌人位置弹青蓝色 Miss
            MissNumber.Show(_targetEnemy.atknumber, _targetEnemy.transform.position);
            return;
        }

        float finalDamage = _damage + (_playerAttr != null ? _playerAttr.atk : 0f);
        bool isCrit = false;
        if (_playerAttr != null && _playerAttr.CR > UnityEngine.Random.value * 100f)
        {
            finalDamage *= _playerAttr.CD / 100f;
            isCrit = true;
        }
        finalDamage -= _targetEnemy.def;
        if (finalDamage < 1f) finalDamage = 1f;

        int dealt = (int)finalDamage;

        // 会话伤害追踪（让对局总结能看到地狱火贡献）
        if (GameSessionTracker.Instance != null && _sourceSkill != null)
            GameSessionTracker.Instance.RecordDamage(_sourceSkill.Skillname, dealt);

        _targetEnemy.health -= dealt;

        if (_targetEnemy.atknumber != null && DamageNumberSettings.Visible)
        {
            GameObject num = Instantiate(_targetEnemy.atknumber, _targetEnemy.transform.position, Quaternion.identity);
            num.transform.localScale *= DamageNumberSettings.SizeScale;
            var txt = num.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            txt.text = ((int)finalDamage).ToString();
            if (isCrit) txt.color = new Color32(255, 215, 0, 255);
        }

        _targetEnemy.startturnred();
        if (_targetEnemy.health <= 0) _targetEnemy.Destroy1();
    }
}
