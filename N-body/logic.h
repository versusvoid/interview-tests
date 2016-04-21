#ifndef LOGIC_H
#define LOGIC_H

#include <boost/lockfree/queue.hpp>
#include <mutex>
#include <thread>

struct Message {

    enum Type {
        Click,

        Drag,
        Move,
        Release,

        Stop,
        Resize,
        Error,
    };

    Message(Type type, int x = 0, int y = 0)
        : type(type)
        , x(x)
        , y(y)
    {}

    Message()
        : type(Type::Error)
        , x(-1)
        , y(-1)
    {}

    Type type;
    int x;
    int y;

};

struct Particle {
    static const double Radius;

    double x;
    double y;
    double vx;
    double vy;
};

class Logic
{
public:
    static const double dt;

    Logic(uint32_t N = 10);

    void start(int width, int height);

    void finish();


    boost::lockfree::queue<Message> messageQueue;

    std::vector<Particle >* readBuffer;

    std::mutex swap_mutex;

private:
    std::vector<Particle >* writeBuffer;
    double width;
    double height;
    std::size_t dragged;
    std::thread workerThread;

    void run();
    void updateParticles();
    std::size_t findParticle(int x, int y);
    bool processMessages();
    void swapBuffers();
};

#endif // LOGIC_H
