using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;

public class ChatSystem : MonoBehaviour
{
    [Header("Chat Settings")]
    public int MaxChatHistory = 100;
    public float LocalChatRange = 30f;

    [Header("Audio")]
    public AudioClip ChatNotificationSound;

    private List<NetworkMessages.ChatMessage> _chatHistory = new List<NetworkMessages.ChatMessage>();
    private AudioSource _audioSource;

    private void Start()
    {
        NetworkManager.OnChatMessage += HandleIncomingMessage;
        _audioSource = GetComponent<AudioSource>();
        
        // Add AudioSource if it doesn't exist
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void HandleIncomingMessage(NetworkMessages.ChatMessage message)
    {
        // Add to chat history
        _chatHistory.Add(message);
        
        // Limit history size
        if (_chatHistory.Count > MaxChatHistory)
        {
            _chatHistory.RemoveAt(0);
        }

        // Play chat sound for certain channels
        if (message.ChannelType == "Private" && message.TargetId == GameManager.Instance.LocalPlayerId)
        {
            // Play private message sound
            PlayChatNotificationSound();
        }
        else if (message.ChannelType == "Global" || message.ChannelType == "Local")
        {
            // Play general chat sound (softer)
            PlayChatNotificationSound(0.5f);
        }

        Debug.Log($"Chat received: [{message.ChannelType}] {message.SenderName}: {message.Message}");
    }

    public List<NetworkMessages.ChatMessage> GetChatHistory(string channelType = null)
    {
        if (string.IsNullOrEmpty(channelType))
        {
            return _chatHistory;
        }
        
        return _chatHistory.Where(msg => msg.ChannelType == channelType).ToList();
    }

    public async Task SendMessage(string message, string channelType, string targetId = null)
    {
        if (string.IsNullOrEmpty(message.Trim())) return;

        if (GameManager.Instance.NetworkManager != null)
        {
            await GameManager.Instance.NetworkManager.SendChatMessage(message, channelType, targetId);
        }
    }

    public List<NetworkMessages.ChatMessage> GetRecentMessages(int count = 10)
    {
        return _chatHistory.TakeLast(count).ToList();
    }

    public void ClearChatHistory()
    {
        _chatHistory.Clear();
        Debug.Log("Chat history cleared");
    }

    public bool IsPlayerMuted(string playerId)
    {
        // Placeholder for mute functionality
        // In a full implementation, this would check a mute list
        return false;
    }

    private void PlayChatNotificationSound(float volume = 1f)
    {
        if (_audioSource != null && ChatNotificationSound != null)
        {
            _audioSource.volume = volume;
            _audioSource.PlayOneShot(ChatNotificationSound);
        }
    }

    // Method to send different types of messages
    public async Task SendGlobalMessage(string message)
    {
        await SendMessage(message, "Global");
    }

    public async Task SendLocalMessage(string message)
    {
        await SendMessage(message, "Local");
    }

    public async Task SendPrivateMessage(string message, string targetPlayerId)
    {
        await SendMessage(message, "Private", targetPlayerId);
    }

    private void OnDestroy()
    {
        NetworkManager.OnChatMessage -= HandleIncomingMessage;
    }
}