
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BMG_MicroTextureAnalyzer;

namespace BMG_MicroTextureAnalyzer_Console
{
    class Program
    {
        static void Main(string[] args)
        {
            var flag = true; 
            do
            {
                //Add my switch statement here
                Engine engine = new Engine();
                
                engine.PropertyChanged += (sender, e) =>
                {
                    Console.WriteLine("Property Changed: ", e.PropertyName);
                };

                Console.WriteLine("Errors: ", engine.Stage.ErrorMessage, "\n");
                Console.WriteLine("Warnings: ", engine.Stage.WarningMessage, "\n");
                Console.WriteLine("1. Connect to Stage\n2.Calculate Pulse Equivalent");
                var key = Console.ReadKey();
                switch (key.KeyChar)
                {
                    case '1':
                        Console.WriteLine("Connecting to stage...");

  
                        engine.ConnectToStage(8);
                        engine.Stage.Delay(1000);
                        if (engine.GetStageConnectionStatus())
                        {
                            Console.WriteLine("Connected to stage\n");
                            Console.WriteLine("Position: ",engine.GetStagePosition());
                            engine.SetPulseEquivalent(0.0001);
                        }
                        else
                        {
                            Console.WriteLine("Failed to connect to stage");
                        }
                    
                       
                        break;
                    case '2':
                        Console.WriteLine("Calculating Pulse Equivalent...");

                        Task.Run(() =>
                        {
                            Console.WriteLine(engine.Stage.CurrentPosition);
                            engine.SetPulseEquivalent(0.005);
                            Console.WriteLine("PulseEquiv: ", engine.Stage.PulseEquivalent,"\n");
                            Console.WriteLine("Stage Position: ", engine.GetStagePosition(),"\n");
                            Console.WriteLine("Stage Step: ", engine.Stage.CurrentStep, "\n");
                            engine.SetStageSpeed(100);
                            engine.Stage.MoveYAbsolute(1000);   

                        });
                        break;
                    case '3':
                        flag = false;
                        break;
                    default:
                        Console.WriteLine("Invalid input");
                        break;
                }
            } while (flag);
        }
    }
}
