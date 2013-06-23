#include "cinder/app/AppNative.h"
#include "cinder/gl/gl.h"

using namespace ci;
using namespace ci::app;
using namespace std;

class Terrain002App : public AppNative {
  public:
	void setup();
	void mouseDown( MouseEvent event );	
	void update();
	void draw();
};

void Terrain002App::setup()
{
}

void Terrain002App::mouseDown( MouseEvent event )
{
}

void Terrain002App::update()
{
}

void Terrain002App::draw()
{
	// clear out the window with black
	gl::clear( Color( 0, 0, 0 ) ); 
}

CINDER_APP_NATIVE( Terrain002App, RendererGl )
