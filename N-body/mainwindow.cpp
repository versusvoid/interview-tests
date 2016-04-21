#include "mainwindow.h"

#include <QPainter>
#include <iostream>

MainWindow::MainWindow(QWidget *parent)
  : QWidget(parent)
  , logic(10)
  , dragging(false)
  , particlePen(QColor::fromRgb(64,64,64))
  , particleBrush(QColor::fromRgb(154, 242, 255))
{

    this->setAutoFillBackground(true);

    QPalette palette = this->palette();
    palette.setBrush(QPalette::ColorRole::Background, QBrush(QColor::fromRgb(90, 147, 84)));
    this->setPalette(palette);

    logic.start(width(), height());
    startTimer(16);
}

MainWindow::~MainWindow()
{
}

void MainWindow::paintEvent(QPaintEvent * /* event */)
{
    QPainter painter(this);

    painter.setPen(particlePen);
    painter.setBrush(particleBrush);
    painter.setRenderHint(QPainter::Antialiasing, true);

    if (true)
    {
        std::lock_guard<std::mutex> guard(logic.swap_mutex);

        for (const Particle& p : *logic.readBuffer)
        {
            painter.drawEllipse(QRectF(p.x - Particle::Radius, p.y - Particle::Radius,
                                2*Particle::Radius, 2*Particle::Radius));
        }
    }

}

void MainWindow::timerEvent(QTimerEvent * /* event */)
{
    update();
}

void MainWindow::resizeEvent(QResizeEvent *event)
{
    logic.messageQueue.push(Message(Message::Type::Resize, event->size().width(), event->size().height()));
}

void MainWindow::mousePressEvent(QMouseEvent *event)
{
    if (event->button() == Qt::LeftButton)
    {
        dragging = true;
        logic.messageQueue.push(Message(Message::Type::Drag, event->x(), event->y()));
    }
}

void MainWindow::mouseMoveEvent(QMouseEvent *event)
{
    if (dragging)
    {
        logic.messageQueue.push(Message(Message::Type::Move, event->x(), event->y()));
    }
}

void MainWindow::mouseReleaseEvent(QMouseEvent *event)
{
    if (event->button() == Qt::RightButton)
    {
        logic.messageQueue.push(Message(Message::Type::Click, event->x(), event->y()));

    }
    else if (event->button() == Qt::LeftButton)
    {
        dragging = false;
        logic.messageQueue.push(Message(Message::Type::Release, event->x(), event->y()));
    }
}

void MainWindow::closeEvent(QCloseEvent * /* event */)
{
    logic.finish();
}
