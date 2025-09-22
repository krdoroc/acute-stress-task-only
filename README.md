# Acute stress unity task

Knapsack Decision Task. Unity 3D (C#) code.

Game controls:
- Enter Participant ID with ENTER.
- Start the game or resumse after a pause with SPACE.
- Skip to the answer screen of a trial with the UP arrow.
- Select the left and right answer buttons by pressing the LEFT and RIGHT arrows, respectively.
- Move left and right along the likert scales by pressing the LEFT and RIGHT arrows, respectively. Press the UP arrow to select your answer.
- To check their head is in the correct position for the eye tracker to correctly record their gaze press 't'.
- To initiate a new calibration of the gaze tracking press 'c'. 

Input/Output data folders are located in Assets/DataInf. This folder has to be added manually to the game after building. 

This is the structure of the folder:
- DataInf
	- Output
	- Input 
		- layoutParam.txt
		- param.txt
		- KPInstances
			- i1.txt
			- i2.txt 
				…
			- 1_param2.txt
			- 2_param2.txt
				…

Description of INPUT files:




Input Files: `param.txt`, `n_param2.txt`, `layoutParam.txt`, `KPInstances/i1.txt`…

The main structure of these files is: 
NameOfTheVariable1:Value1
NameOfTheVariable2:Value2
…

layoutParam.txt
Relevant Parameters for the layout of the screen. All Variables must be INTEGERS.
columns:=number of columns in the grid were to lay randomly the items.
rows:= number of rows in the grid were to lay randomly the items.
randomPlacementType:=The method to be used to place items randomly on the grid.
	1:Choose random positions from full grid. It might happen that no placement is found and the trial will be skipped.
	2. Choose randomly out of 10 positions. A placement is guaranteed. For this placement type it must be that columns=16 and rows=12. Alternatively, delete these 	variables from the layout.txt file
totalAreaBill:=The total area of all the value items. A good initialisation for this variables is the number of items plus 1.
totalAreaWeight:=The total area of all the weight items. A good initialisation for this variables is the number of items plus 1.


param.txt
Relevant Parameters of the task. All Variables must be INTEGERS or vectors of INTEGERS.
timeRest1min:=Minimum time for the randomised inter-trial Break.
timeRest1max:=Maximum time for the randomised inter-trial Break.
timeRest2:=Time for the inter-blocks Break.
timeQuestion:=Maximum time looking at the trial/instance/stimuli.
timeAnswer:=Maximum time to submit an answer on the answer screen.
timeLikert:=Maximum time to submit a likert rating.
saliva_time:=Intervals of time (minutes) after which the task automatically pauses at the next opportunity. e.g., [14,20,20] will pause the task 14 minutes after starting the first trial, 20 minutes after the first pause, and 20 minutes after the second pause. 

KPInstances/n_param2.txt 
Variables can be allocated between param.txt and param2.txt with no effect on the game; however there must not be repeated definitions of variables. The distinction is done because param2.txt is an output from the instance selection program (e.g python).
numberOfInstances:=Number of instances to be imported. The files uploaded are 			automatically i1-i”numberOfInstances”
numberOfBlocks:=Number of blocks.
numberOfTrials:=Number of trials in each block.
instanceRandomization:=Sequence of instances to be randomised. The vector must have length: 	trials*blocks. E.g. [1,3,2,3,1,3,1,2,3,1] for 2 blocks of 5 trials.


KPInstances/i1.txt,KPInstances/i2.txt,…
Instance information. Each file is a different instance of the Knapsack problem. 
Files must be added sequentially (i.e. 1,2,3,…). All Variables must be INTEGERS or vectors of INTEGERS.
Weights and values must be vectors with the same length. InstanceType should be allocated according to one of the levels of difficulty. 

Description of fields:
instance_id:=Unique instance identifier
capacity:=Capacity constraint
profit:=Profit target
values:=Vector of item values
weights:=Vector of item weights
opt_profit:=Maximum attainable profit given the items and weight constraint
alpha_star:0.46969696969697
solution:=1 if the solution is yes, 0 if no
alpha_p_wanted:=Target level of normalised profit
alpha_p_sampled:=Actual normalised profit
alpha_c_wanted:=Target for normalised capacity
alpha_c_sampled:=Actual nromalised capacity
ICdist_wanted:=Target instance complexity
ICdist_sampled:=Actual instance complexity
seed:=Random seed used to generate the instance

Sample Instance:

instance_id:1
capacity:94
profit:98
values:[46,33,21,24,32,38,49,21]
weights:[32,25,14,20,42,42,39,20]
opt_profit:124
alpha_star:0.46969696969697
solution:1
alpha_p_wanted:0.36969696969697
alpha_p_sampled:0.371212121212121
alpha_c_wanted:0.4
alpha_c_sampled:0.401709401709402
ICdist_wanted:-0.1
ICdist_sampled:-0.0984848484848485
seed:1637


Key information is in the `Assets` folder. Important folders include:

1. `_scenes`: the different screens participants saw when completing the task.
  1. `SetUp`: Participant clicks on the input field and types in their ParticipantID. They press Enter to submit their PID, and then spacebar to begin the first trial.
  2. `Trial`: Displays one instance of the knapsack task. Participant sees the timer deplete to let them know how much time is remaining, otherwise the screen is static. Participants can press the 'up' arrow to forgo their remaining time and proceed immediately to the answer screen.
  3. `TrialAnswer`: Displays the answer screen, which shows a box on the left and right of the screen each containing either Yes or No (the two possible solutions). Participants press the 'left' ('right') arrow to select the answer visible on the left (right) of the screen.
  4. `ComplexityLikert`: Upon answering a trial participants rate its difficulty on a likert scale. They move along the scale with the 'left' and 'right' arrow keys in the expected direction. They press the 'up' arrow key to submit their final rating.
  5. `StressLikert`: Every few trials participants rate their stress levels on a likert scale. They move along the scale with the 'left' and 'right' arrow keys in the expected direction. They press the 'up' arrow key to submit their final rating.
  6. `InterTrialRest`: Short rest screen in between trials, participants see nothing but a fixation cross.
  7. `SalivaPause`: At set points in time, the game will automatically pause when it is time to take a saliva sample. The pause will wait until a trial has been completed (including its ratings) before beginning. Following the sample, participants press the spacebar to end the pause and resume the task.
  8. `End`: Upon completing the last trial participants see a screen informing them they've completed the task.
2. `DataInf`: stores all of the input parameters.
3. `Scripts`: stores the C# scripts that run the game
  1. `BoardManager`: script mostly responsible for placing objects on the screen and visual appearance
  2. `GameManager`: primary engine responsible for most everything else
4. `TobiiPro/Common/Scripts`: stores the C# scripts required for eye tracking and integration with TobiiPro
5. `ScreenBasedSaveData.cs`: script responsible for saving the eye-tracking files. 
