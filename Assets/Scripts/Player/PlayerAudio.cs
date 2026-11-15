
using UnityEngine;

public class PlayerAudio : MonoBehaviour
{
    [Header("Audio Source")]
    [SerializeField] private AudioSource audioSource;

    [Header("Footsteps")]
    [SerializeField] private AudioClip[] walkSounds;
    [SerializeField] private AudioClip[] runSounds;
    [SerializeField, Range(0f, 1f)] private float walkVolume = 0.4f;
    [SerializeField, Range(0f, 1f)] private float runVolume = 0.6f;

    [Header("Movement")]
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip landSound;

    [Header("Combat")]
    [SerializeField] private AudioClip[] damageSounds;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip[] impactSounds;

    [Header("Items")]
    [SerializeField] private AudioClip pickupHealSound;
    [SerializeField] private AudioClip shieldSound;

    [Header("Volume Settings")]
    [SerializeField, Range(0f, 1f)] private float movementVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float combatVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float itemVolume = 0.7f;

    [Header("Pitch Variation")]
    [SerializeField] private bool randomPitch = true;
    [SerializeField] private float minPitch = 0.9f;
    [SerializeField] private float maxPitch = 1.1f;

    [Header("Footstep Timing")]
    [SerializeField] private float walkInterval = 0.45f;
    [SerializeField] private float runInterval = 0.28f;
    [SerializeField] private bool enableAudio;

    private float footstepTimer;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    // GENERAL AUDIO METHODS

    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null || audioSource == null)
            return;

        float originalPitch = audioSource.pitch;

        if (randomPitch)
        {
            audioSource.pitch = Random.Range(minPitch, maxPitch);
        }

        audioSource.PlayOneShot(clip, volume);

        audioSource.pitch = originalPitch;
    }

    private void PlayRandomSound(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0)
            return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];

        PlaySound(clip, volume);
    }

    // FOOTSTEPS - ANIMATION EVENTS

    public void PlayWalkStep()
    {
        if(!enableAudio) return;
        PlayRandomSound(walkSounds, walkVolume);
    }

    public void PlayRunStep()
    {
        if(!enableAudio) return;
        PlayRandomSound(runSounds, runVolume);
    }

    // MOVEMENT

    public void PlayJump()
    {
        PlaySound(jumpSound, movementVolume);
    }

    public void PlayLand()
    {
        PlaySound(landSound, movementVolume);
    }

    // COMBAT

    public void PlayDamage()
    {
        PlayRandomSound(damageSounds, combatVolume);
    }

    public void PlayDeath()
    {
        PlaySound(deathSound, combatVolume);
    }

    public void PlayShoot()
    {
        PlaySound(shootSound, combatVolume);
    }

    public void PlayImpact()
    {
        PlayRandomSound(impactSounds, combatVolume);
    }

    // ITEMS

    public void PlayPickup()
    {
        PlaySound(pickupHealSound, itemVolume);
    }

    public void PlayShield()
    {
        PlaySound(shieldSound, itemVolume);
    }

    // CUSTOM SOUND

    public void PlayCustom(AudioClip clip)
    {
        PlaySound(clip, 1f);
    }

}
