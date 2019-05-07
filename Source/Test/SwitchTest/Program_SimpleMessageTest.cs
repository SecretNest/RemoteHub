using System;
using System.Collections.Generic;
using System.Text;

namespace SwitchTest
{
    partial class Program
    {
        static void SimpleMessageTest()
        {
            while (true)
            {
                Console.WriteLine("From: 0/1/2/3 other to quit...");
                if (!TryGetClientIndex(out int sourceIndex))
                {
                    break;
                }
                var client = clients[sourceIndex];
                Console.WriteLine("To: 0/1/2/3 other to quit...");
                if (!TryGetClientIndex(out int targetIndex))
                {
                    break;
                }
                var target = clients[targetIndex].ClientId; //Get Id only. Not related to any operating on target client.
                Console.WriteLine("From: {0} To: {1}", sourceIndex, targetIndex);
                client.SendMessage(target, "<-- Test Message -->"); //from the source, to the target
            }
        }

        static bool TryGetClientIndex(out int index)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.D0 || key.Key == ConsoleKey.NumPad0)
            {
                index = 0;
                return true;
            }
            else if (key.Key == ConsoleKey.D1 || key.Key == ConsoleKey.NumPad1)
            {
                index = 1;
                return true;
            }
            else if (key.Key == ConsoleKey.D2 || key.Key == ConsoleKey.NumPad2)
            {
                index = 2;
                return true;
            }
            else if (key.Key == ConsoleKey.D3 || key.Key == ConsoleKey.NumPad3)
            {
                index = 3;
                return true;
            }
            else
            {
                index = -1;
                return false;
            }
        }
    }
}
