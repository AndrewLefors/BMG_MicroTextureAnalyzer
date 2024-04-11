namespace BMG_MicroTextureAnalyzer_GUI
{
    using BMG_MicroTextureAnalyzer;
    public partial class Form1 : Form
    {
        public MotionController mc = new BMG_MicroTextureAnalyzer.MotionController();
        public Form1()
        {
            //BMG_MicroTextureAnalyzer.MotionController mc = new BMG_MicroTextureAnalyzer.MotionController();
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Console.WriteLine("Button clicked");
            //BMG_MicroTextureAnalyzer.MotionController mc = new BMG_MicroTextureAnalyzer.MotionController();
            this.mc.ConnectPort(8);
            if (mc.ConnectionStatus)
            {
                textBox1.Text = "Connected successfully";
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            // Disable the button to prevent multiple clicks
            button2.Enabled = false;
            textBox1.Text = "Connecting...";

            // Run the operation in a separate thread
            Task.Run(() =>
            {
                BMG_MicroTextureAnalyzer.MotionController mc = new BMG_MicroTextureAnalyzer.MotionController();
                mc.ConnectPort(8);

                // Now we need to marshal the UI updates back to the UI thread
                this.Invoke(new Action(() =>
                {
                    if (mc.ConnectionStatus)
                    {
                        textBox1.Text = "Connected";
                        mc.PulseEquivalent = 0.005;
                        // Additional operations can go here
                        // mc.MoveYStageAbsolute(200);

                        Task.Run(() =>
                        {
                            mc.MoveYStageAbsolute(200);
                        });
                    }
                    else
                    {
                        textBox1.Text = "Failed to connect";
                    }
                    // Re-enable the button after operation is complete
                    button2.Enabled = true;
                }));
                
            });
   
        }


        private void StopButton_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                mc.StopStage();
            });
        }
    }
}
