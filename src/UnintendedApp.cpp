#include "cinder/app/AppNative.h"
#include "cinder/gl/gl.h"

using namespace ci;
using namespace ci::app;
using namespace std;

class UnintendedApp : public AppNative {
  public:
	void setup();
	void mouseDown( MouseEvent event );	
	void update();
	void draw();
};

void UnintendedApp::setup()
{
}

void UnintendedApp::mouseDown( MouseEvent event )
{
}

void UnintendedApp::update()
{
}

void UnintendedApp::draw()
{
	// clear out the window with black
	gl::clear( Color( 0, 0, 0 ) ); 
}

CINDER_APP_NATIVE( UnintendedApp, RendererGl )
