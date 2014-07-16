Dump Memory for Visual Studio
=============================

How hard could it be to read/write debug target memory in Visual Studio? That 
was the question that led me down the rabbit-hole of VS internals and the pile 
of spaghetti that is MSDN documentation. This plugin (which is only tested for 
VS2010 but may support VS2011 and VS2012 too) uses a lot of hacky techniques to 
steal information from Visual Studio internals in order to be able to access 
the debug target's memory and allow you to dump the memory into a raw binary 
file or load a raw binary into the debugee memory space.

### Usage

Open the tool window with Tools -> Dump Memory. Select Dump Memory or Load 
Memory at the bottom. Select the input/output file. Type in a valid VS debug 
expression for address (that means an integer "1024", hex "0x1000", or anything 
that VS recognizes as an integer "0x100+128*3"). Type in a number (if hex, it 
has to start with "0x") for the length if dumping and press Go.

### Inspirations

[Macropolygon](http://macropolygon.wordpress.com/2012/12/16/evaluating-debugged-process-memory-in-a-visual-studio-extension/)
Very similar to what I wanted to do, but was incomplete in implementation

[Image Watch extension](http://visualstudiogallery.msdn.microsoft.com/e682d542-7ef3-402c-b857-bbfba714f78d)
An offical Microsoft extension that suprisingly did most of the same hacky 
things I had to do to get access to raw memory. Parts were stolen from their 
implementation. Guess even Microsoft had to fall victim to Microsoft's crappy 
limited documentation and APIs.

### License (BSD 3-Clause)

Copyright (c) 2014, Yifan Lu
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this 
list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice, 
this list of conditions and the following disclaimer in the documentation and/or
other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its contributors 
may be used to endorse or promote products derived from this software without 
specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND 
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED 
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES 
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; 
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON 
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT 
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS 
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
