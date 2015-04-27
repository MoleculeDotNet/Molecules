# Molecule.Net Support Software

## Introduction
Molecule.Net is a collection of processors and sub-assemblies that run the Microsoft .Net Micro Framework runtime.
This project contains the source code for various support packages designed for use with the Molecule.Net family
of products.

The Molecule family includes:

* Oxygen - An extremely compact STM32F411-based processor breakout board
* Neon - An ESP8266-based wifi module
* Hydrogen - BLE113-based Bluetooth Low-Energy module
* Helium - A radio-frequency serial interconnect module supporting point-to-point and star networks
* Nickel - A power booster allowing the use of low-voltage NiCD battery packs
* Carbon - To be announced
* Silicon - To be announced
And more to come...

## How to use these projects
Mostly, this code is here for your reference. To build Molecule.Net projects, you should use Visual Studio or MonoDevelop
and install the Microsoft .Net Micro Framework 4.3 SDK and then install the Molecule.Net packages from Nuget.
Search nuget for "IngenuityMicro" either in the package manager
or nuget.org and you will get a list of all the current packages. You can install a package from the package manager
GUI or package manager command-line interface. In some cases, you may have to enable the option for searching "Prerelease" nuget packages
if we haven't released a production version yet or if you want more recent (and consequently less tested
and stable) builds.

Building this project from scratch is not recommended because these projects are oriented toward creating nuget
packages and not toward direct use with end-user solutions. Also, you will miss out on updates and other useful
facilities provided by nuget-based package installation.

## Installing from Nuget GUI

### Visual Studio

1. Create a new Microsoft .Net Micro Framework project
1. Right-click on the solution and select "Manage Nuget Packages for Solution"
1. Enter "IngenuityMicro" in the search box
1. Change the "Stable Only" selection to "Include Prerelease" if you don't see the package you want or if you want
to try the latest (and less stable) builds.
1. Select the support package you want (Neon for instance). Nuget will install all of the dependencies.  For instance,
for Neon, it will also install the Oxygen and IngenuityMicro.Net packages.
1. You may also want to right-click on your solution and select "Enable Nuget package restore", which will install
the nuget packages automatically if you move your project to a different computer.

## Code Samples and Documentation

Code samples and documentation will be coming soon to the github wiki

